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
    
    public class WorkflowResult
    {
        public static string Path => "/workflow/completed";
    
        [JsonProperty("identifier")]
        public string Identifier { get; set; }
    
        [JsonProperty("result")]
        public string Result { get; set; }

        public static WorkflowResult Make(string identifier, string result) => new ()
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

    public interface IBridge : IBridgeMessenger
    {
        IObservable<BridgeMessage> Publish(string path);
        
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
        private Dictionary<string, Subject<BridgeMessage>> _subjects = new ();
        
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

        public void SendMessage(string path, string content)
        {
            _messenger.SendMessage(path, content);
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
        private readonly Dictionary<string, IWorkflowImplementation> incomingWorkflowImplementations = new();
        private readonly Dictionary<string, CancellationTokenSource> incomingCancellationSources = new();

        private IBridge _bridge;
        private CompositeDisposable _subscriptions = new();
        
        public BridgeWorkflowController(IBridge bridge)
        {
            _bridge = bridge;

            void RunSync(ISyncWorkflowImplementation implementation, WorkflowRequest request)
            {
                try
                {
                    string content = implementation.Perform(request.Payload);
                    WorkflowResult result = WorkflowResult.Make(request.Identifier, content);
                    _bridge.SendMessage(WorkflowResult.Path, JsonConvert.SerializeObject(result, _bridge.JsonSerializerSettings));
                }
                catch (Exception e)
                {
                    WorkflowFailure failure = WorkflowFailure.Make(request.Identifier, e);
                    _bridge.SendMessage(WorkflowFailure.Path, JsonConvert.SerializeObject(failure, _bridge.JsonSerializerSettings));
                }
            }
            
            async UniTask RunAsync(IAsyncWorkflowImplementation implementation, WorkflowRequest request)
            {
                CancellationTokenSource source = new CancellationTokenSource();
                incomingCancellationSources[request.Identifier] = source;
                try
                {
                    string content = await implementation.Perform(request.Payload, source.Token);
                    WorkflowResult result = WorkflowResult.Make(request.Identifier, content);
                    _bridge.SendMessage(WorkflowResult.Path, JsonConvert.SerializeObject(result, _bridge.JsonSerializerSettings));
                }
                catch (Exception e)
                {
                    WorkflowFailure failure = WorkflowFailure.Make(request.Identifier, e);
                    _bridge.SendMessage(WorkflowFailure.Path, JsonConvert.SerializeObject(failure, _bridge.JsonSerializerSettings));
                }
                finally
                {
                    incomingCancellationSources.Remove(request.Identifier);
                }
            }
            
            _bridge.PublishContent<WorkflowRequest>(WorkflowRequest.Path).Subscribe(request =>
            {
                if (!incomingWorkflowImplementations.TryGetValue(request.Procedure, out IWorkflowImplementation implementation))
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
                if (incomingCancellationSources.TryGetValue(cancellation.Identifier, out CancellationTokenSource source))
                {
                    source.Cancel();
                    incomingCancellationSources.Remove(cancellation.Identifier);
                }
            });
        }

        public async UniTask<TResult> PerformWorkflow<TPayload, TResult>(string procedure, TPayload payload)
        {
            // Implementation of the method...
            throw new NotImplementedException();
        }

        public async UniTask<WorkflowResult> PerformWorkflow<TPayload>(string procedure, TPayload payload)
        {
            // Implementation of the method...
            throw new NotImplementedException();
        }

        public void Register<TPayload, TResult>(string procedure, Func<TPayload, UniTask<TResult>> callback)
        {
            // Implementation of the method...
            throw new NotImplementedException();
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
