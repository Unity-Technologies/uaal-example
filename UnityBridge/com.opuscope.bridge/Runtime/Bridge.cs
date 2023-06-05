using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UniRx;

namespace Opuscope.Bridge
{
    public class BridgeMessage
    {
        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }
    }
    
    public class WorkflowCompletion
    {
        public static string Path => "/workflow/completed";
    
        [JsonProperty("identifier")]
        public string Identifier { get; set; }
    
        [JsonProperty("result")]
        public string Result { get; set; }

        public static WorkflowCompletion Make(string identifier, string result) => new ()
        {
            Identifier = identifier,
            Result = result
        };
    }

    public class WorkflowFailure
    {
        public static string Path => "/workflow/failed";
    
        [JsonProperty("identifier")]
        public string Identifier { get; set; }
    
        [JsonProperty("type")]
        public string Type { get; set; }
    
        [JsonProperty("message")]
        public string Message { get; set; }
        
        public static WorkflowFailure Make(string identifier, Exception e)
        {
            return new WorkflowFailure();
        }

        public Exception ToException()
        {
            return new Exception();
        }
    }

    public class WorkflowRequest
    {
        public static string Path => "/workflow/request";
    
        [JsonProperty("identifier")]
        public string Identifier { get; set; }
    
        [JsonProperty("procedure")]
        public string Procedure { get; set; }
    
        [JsonProperty("payload")]
        public string Payload { get; set; }
    }

    public class WorkflowCancellation
    {
        public static string Path => "/workflow/cancel";
    
        [JsonProperty("identifier")]
        public string Identifier { get; set; }
    }

    public interface IWorkflowImplementation { }
    
    public interface ISyncWorkflowImplementation : IWorkflowImplementation
    {
        string Perform(string payload);
    }
    
    public interface IAsyncWorkflowImplementation : IWorkflowImplementation
    {
        UniTask<string> Perform(string payload, CancellationToken cancellationToken);
    }

    public interface IBridge
    {
        IObservable<BridgeMessage> Publish(string path);

        void Send<T>(string path, T content);
        
        JsonSerializerSettings JsonSerializerSettings { get; }
    }
    
    public static class BridgeExtensions
    {
        public static IObservable<T> PublishContent<T>(this IBridge bridge, string path, JsonSerializerSettings jsonSerializerSettings = null) where T : class
        {
            return bridge.Publish(path).Decode<T>(jsonSerializerSettings);
        }
    }
    
    public class Bridge : IBridge, IDisposable
    {
        private readonly IBridgeMessenger _messenger;
        private IDisposable _notificationSubscription;
        private readonly Dictionary<string, Subject<BridgeMessage>> _subjects = new ();
        
        public JsonSerializerSettings JsonSerializerSettings { get; }

        public Bridge(IBridgeMessenger messenger, IBridgeListener listener, JsonSerializerSettings _serializerSettings = null)
        {
            JsonSerializerSettings = _serializerSettings ?? new JsonSerializerSettings();
            _messenger = messenger;
            _notificationSubscription = listener.Notifications.Subscribe(notification =>
            {
                if (_subjects.TryGetValue(notification.Path, out Subject<BridgeMessage> subject))
                {
                    subject.OnNext(notification);
                }
            });
        }

        public void Dispose()
        {
            foreach (Subject<BridgeMessage> subject in _subjects.Values)
            {
                subject.Dispose();
            }
            _subjects.Clear();
            _notificationSubscription?.Dispose();
            _notificationSubscription = null;
        }

        public void Send<T>(string path, T content)
        {
            string serialized = JsonConvert.SerializeObject(content, JsonSerializerSettings);
            _messenger.SendMessage(path, serialized);
        }

        public IObservable<BridgeMessage> Publish(string path)
        {
            if (_subjects.TryGetValue(path, out Subject<BridgeMessage> subject))
            {
                return subject.AsObservable();
            }
            subject = new Subject<BridgeMessage>();
            _subjects[path] = subject;
            return subject.AsObservable();
        }
    }
    
    public class BridgeWorkflowController : IDisposable
    {
        private readonly Dictionary<string, IWorkflowImplementation> _workflowImplementations = new();
        private readonly Dictionary<string, CancellationTokenSource> _cancellationSources = new();
        
        private readonly Dictionary<string, UniTaskCompletionSource<WorkflowCompletion>> _completionSources = new();

        private readonly IBridge _bridge;
        private CompositeDisposable _subscriptions = new();
        
        public BridgeWorkflowController(IBridge bridge)
        {
            _bridge = bridge;
            void RunSync(ISyncWorkflowImplementation implementation, WorkflowRequest request)
            {
                try
                {
                    string content = implementation.Perform(request.Payload);
                    WorkflowCompletion completion = WorkflowCompletion.Make(request.Identifier, content);
                    _bridge.Send(WorkflowCompletion.Path, completion);
                }
                catch (Exception e)
                {
                    WorkflowFailure failure = WorkflowFailure.Make(request.Identifier, e);
                    _bridge.Send(WorkflowFailure.Path, failure);
                }
            }
            async UniTask RunAsync(IAsyncWorkflowImplementation implementation, WorkflowRequest request)
            {
                CancellationTokenSource source = new CancellationTokenSource();
                _cancellationSources[request.Identifier] = source;
                try
                {
                    string content = await implementation.Perform(request.Payload, source.Token);
                    WorkflowCompletion completion = WorkflowCompletion.Make(request.Identifier, content);
                    _bridge.Send(WorkflowCompletion.Path, completion);
                }
                catch (Exception e)
                {
                    WorkflowFailure failure = WorkflowFailure.Make(request.Identifier, e);
                    _bridge.Send(WorkflowFailure.Path, failure);
                }
                finally
                {
                    _cancellationSources.Remove(request.Identifier);
                }
            }
            _bridge.PublishContent<WorkflowRequest>(WorkflowRequest.Path).Subscribe(request =>
            {
                if (!_workflowImplementations.TryGetValue(request.Procedure, out IWorkflowImplementation implementation))
                {
                    return;
                }
                switch (implementation)
                {
                    case ISyncWorkflowImplementation syncWorkflowImplementation:
                        RunSync(syncWorkflowImplementation, request);
                        break;
                    case IAsyncWorkflowImplementation asyncWorkflowImplementation:
                        RunAsync(asyncWorkflowImplementation, request).Forget();
                        break;
                }
            });
            _bridge.PublishContent<WorkflowCancellation>(WorkflowCancellation.Path).Subscribe(cancellation =>
            {
                if (_cancellationSources.TryGetValue(cancellation.Identifier, out CancellationTokenSource source))
                {
                    source.Cancel();
                    _cancellationSources.Remove(cancellation.Identifier);
                }
            });
            _bridge.PublishContent<WorkflowCompletion>(WorkflowCompletion.Path).Subscribe(completion =>
            {
                if (_completionSources.TryGetValue(completion.Identifier, out UniTaskCompletionSource<WorkflowCompletion> source))
                {
                    source.TrySetResult(completion);
                    _completionSources.Remove(completion.Identifier);
                }
            });
            _bridge.PublishContent<WorkflowFailure>(WorkflowFailure.Path).Subscribe(failure =>
            {
                if (_completionSources.TryGetValue(failure.Identifier, out UniTaskCompletionSource<WorkflowCompletion> source))
                {
                    source.TrySetException(failure.ToException());
                    _completionSources.Remove(failure.Identifier);
                }
            });
        }

        public UniTask<TResult> PerformWorkflow<TPayload, TResult>(string procedure, TPayload payload)
        {
            return PerformWorkflow<TPayload, TResult>(procedure, payload, CancellationToken.None);
        }
        
        public async UniTask<TResult> PerformWorkflow<TPayload, TResult>(string procedure, TPayload payload, CancellationToken cancellationToken)
        {
            using CompositeDisposable subscriptions = new CompositeDisposable();

            string identifier = Guid.NewGuid().ToString();
            UniTaskCompletionSource<TResult> taskCompletionSource = new UniTaskCompletionSource<TResult>();
            
            // TODO : find a better way of observing cancellation token
            subscriptions.Add(Observable.EveryUpdate().Where(_ => cancellationToken.IsCancellationRequested)
                .First()
                .Subscribe(_ => _bridge.Send(WorkflowCancellation.Path, new WorkflowCancellation { Identifier = identifier })));
            
            subscriptions.Add(_bridge.PublishContent<WorkflowCompletion>(WorkflowCompletion.Path)
                .Where(completion => completion.Identifier == identifier)
                .Subscribe(completion =>
                {
                    TResult result = JsonConvert.DeserializeObject<TResult>(completion.Result);
                    if (result == null)
                    {
                        taskCompletionSource.TrySetException(new Exception("result serialization failure"));
                    }
                    else
                    {
                        taskCompletionSource.TrySetResult(result);
                    }
                }));
            
            string serialized = JsonConvert.SerializeObject(payload, _bridge.JsonSerializerSettings);
            WorkflowRequest request = new WorkflowRequest
            {
                Identifier = identifier,
                Procedure = procedure,
                Payload = serialized
            };
            _bridge.Send(WorkflowRequest.Path, request);

            // note : important to explicitly await so that the using subscriptions stay alive
            TResult result = await taskCompletionSource.Task;
            return result;
        }

        private void ThrowIfConflictingProcedure(string procedure)
        {
            if (_workflowImplementations.ContainsKey(procedure))
            {
                throw new Exception("conflicting procedure : " + procedure);
            }
        }
        
        public void Register<TPayload, TResult>(string procedure, Func<TPayload, CancellationToken, UniTask<TResult>> callback)
        {
            ThrowIfConflictingProcedure(procedure);
            _workflowImplementations[procedure] = new AsyncWorkflowImplementation<TPayload, TResult>(
                callback, _bridge.JsonSerializerSettings);
        }
        
        public void Register<TPayload, TResult>(string procedure, Func<TPayload, UniTask<TResult>> callback)
        {
            ThrowIfConflictingProcedure(procedure);
            _workflowImplementations[procedure] = new AsyncWorkflowImplementation<TPayload, TResult>(
                (payload, _) => callback(payload), _bridge.JsonSerializerSettings);
            
        }
        
        public void Register<TPayload, TResult>(string procedure, Func<TPayload, TResult> callback)
        {
            ThrowIfConflictingProcedure(procedure);
            _workflowImplementations[procedure] = new SyncWorkflowImplementation<TPayload, TResult>(
                callback, _bridge.JsonSerializerSettings);
        }

        private class SyncWorkflowImplementation<TPayload, TResult> : ISyncWorkflowImplementation
        {
            private readonly Func<TPayload, TResult> _callback;
            private readonly JsonSerializerSettings _serializerSettings;

            public SyncWorkflowImplementation(Func<TPayload, TResult> callback, JsonSerializerSettings serializerSettings)
            {
                _callback = callback;
                _serializerSettings = serializerSettings;
            }

            public string Perform(string payload)
            {
                TPayload deserialized = JsonConvert.DeserializeObject<TPayload>(payload, _serializerSettings);
                TResult result = _callback(deserialized);
                return JsonConvert.SerializeObject(result, _serializerSettings);
            }
        }
        
        private class AsyncWorkflowImplementation<TPayload, TResult> : IAsyncWorkflowImplementation
        {
            private readonly Func<TPayload, CancellationToken, UniTask<TResult>> _callback;
            private readonly JsonSerializerSettings _serializerSettings;

            public AsyncWorkflowImplementation(Func<TPayload, CancellationToken, UniTask<TResult>> callback, JsonSerializerSettings serializerSettings)
            {
                _callback = callback;
                _serializerSettings = serializerSettings;
            }

            public async UniTask<string> Perform(string payload, CancellationToken cancellationToken)
            {
                TPayload deserialized = JsonConvert.DeserializeObject<TPayload>(payload, _serializerSettings);
                TResult result = await _callback(deserialized, cancellationToken);
                return JsonConvert.SerializeObject(result, _serializerSettings);
            }
        }

        public void Dispose()
        {
            _subscriptions?.Dispose();
            _subscriptions = null;
        }
    }

    public interface IBridgeListener
    {
        IObservable<BridgeMessage> Notifications { get; }
    }

    public interface IBridgeMessenger
    {
        void SendMessage(string path, string content);
    }
}
