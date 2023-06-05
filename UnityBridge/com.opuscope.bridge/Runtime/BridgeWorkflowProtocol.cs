using System;
using Newtonsoft.Json;

namespace Opuscope.Bridge
{
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
}