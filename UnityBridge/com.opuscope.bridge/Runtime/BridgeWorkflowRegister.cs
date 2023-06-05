using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UniRx;

namespace Opuscope.Bridge
{
    public interface IWorkflowImplementation { }
    
    public interface ISyncWorkflowImplementation : IWorkflowImplementation
    {
        string Perform(string payload);
    }
    
    public interface IAsyncWorkflowImplementation : IWorkflowImplementation
    {
        UniTask<string> Perform(string payload, CancellationToken cancellationToken);
    }
    
    public class BridgeWorkflowRegister
    {
        private readonly Dictionary<string, IWorkflowImplementation> _workflowImplementations = new();
        private readonly Dictionary<string, CancellationTokenSource> _cancellationSources = new();
        
        private readonly IBridge _bridge;
        private CompositeDisposable _subscriptions = new();
        
        public BridgeWorkflowRegister(IBridge bridge)
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
}