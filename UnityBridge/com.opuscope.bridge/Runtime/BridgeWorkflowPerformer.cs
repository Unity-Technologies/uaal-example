using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UniRx;

namespace Opuscope.Bridge
{
    public class BridgeWorkflowPerformer : IDisposable
    {
        private readonly Dictionary<string, UniTaskCompletionSource<WorkflowCompletion>> _completionSources = new();
        
        private readonly IBridge _bridge;
        private readonly CompositeDisposable _subscriptions = new();
        
        public BridgeWorkflowPerformer(IBridge bridge)
        {
            _bridge = bridge;
            _subscriptions.Add(_bridge.PublishContent<WorkflowCompletion>(WorkflowCompletion.Path).Subscribe(completion =>
            {
                if (_completionSources.TryGetValue(completion.Identifier, out UniTaskCompletionSource<WorkflowCompletion> source))
                {
                    source.TrySetResult(completion);
                    _completionSources.Remove(completion.Identifier);
                }
            }));
            _subscriptions.Add(_bridge.PublishContent<WorkflowFailure>(WorkflowFailure.Path).Subscribe(failure =>
            {
                if (_completionSources.TryGetValue(failure.Identifier, out UniTaskCompletionSource<WorkflowCompletion> source))
                {
                    source.TrySetException(failure.ToException());
                    _completionSources.Remove(failure.Identifier);
                }
            }));
        }

        public async UniTask<TResult> Perform<TPayload, TResult>(string procedure, TPayload payload, CancellationToken cancellationToken)
        {
            WorkflowCompletion completion = await PerformWorkflow(procedure, payload, CancellationToken.None);
            return JsonConvert.DeserializeObject<TResult>(completion.Result);
        }
        
        private async UniTask<WorkflowCompletion> PerformWorkflow<TPayload>(string procedure, TPayload payload, CancellationToken cancellationToken)
        {
            string identifier = Guid.NewGuid().ToString();
            UniTaskCompletionSource<WorkflowCompletion> taskCompletionSource = new UniTaskCompletionSource<WorkflowCompletion>();

            _completionSources[identifier] = taskCompletionSource;
            
            await using CancellationTokenRegistration cancellationAction = cancellationToken.Register(() =>
            {
                _bridge.Send(WorkflowCancellation.Path, new WorkflowCancellation {Identifier = identifier});
            });
            
            string serialized = JsonConvert.SerializeObject(payload, _bridge.JsonSerializerSettings);
            WorkflowRequest request = new WorkflowRequest
            {
                Identifier = identifier,
                Procedure = procedure,
                Payload = serialized
            };
            _bridge.Send(WorkflowRequest.Path, request);

            // note : important to explicitly await so that the using subscriptions stay alive
            return await taskCompletionSource.Task;
        }

        public void Dispose()
        {
            _subscriptions?.Dispose();
        }
    }
}