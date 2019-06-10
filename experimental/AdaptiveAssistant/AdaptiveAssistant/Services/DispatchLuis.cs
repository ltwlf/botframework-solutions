// <auto-generated>
// Code generated by LUISGen C:\Users\lamil\source\repos\AI\experimental\AdaptiveAssistant\AdaptiveAssistant\Deployment\Scripts\..\Resources\Dispatch\en\LamilAssistantTesten_Dispatch.json -cs Luis.DispatchLuis -o C:\Users\lamil\source\repos\AI\experimental\AdaptiveAssistant\AdaptiveAssistant\Services
// Tool github: https://github.com/microsoft/botbuilder-tools
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.
// </auto-generated>
using Newtonsoft.Json;
using System.Collections.Generic;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.AI.Luis;
namespace Luis
{
    public class DispatchLuis: IRecognizerConvert
    {
        public string Text;
        public string AlteredText;
        public enum Intent {
            emailSkill, 
            l_general, 
            q_chitchat, 
            q_faq, 
            toDoSkill, 
            None
        };
        public Dictionary<Intent, IntentScore> Intents;

        public class _Entities
        {
            // Simple entities
            public string[] DirectionalReference;
            public string[] ListType;
            public string[] TaskContent;
            public string[] OrderReference;
            public string[] SenderName;
            public string[] Category;
            public string[] ContactName;
            public string[] Message;
            public string[] EmailSubject;

            // Built-in entities
            public double[] number;
            public double[] ordinal;

            // Lists
            public string[][] FoodOfGrocery;

            // Pattern.any
            public string[] TaskContent_Any;
            public string[] Message_Any;
            public string[] EmailSubject_Any;
            public string[] SearchTexts_Any;

            // Instance
            public class _Instance
            {
                public InstanceData[] DirectionalReference;
                public InstanceData[] ListType;
                public InstanceData[] TaskContent;
                public InstanceData[] OrderReference;
                public InstanceData[] SenderName;
                public InstanceData[] Category;
                public InstanceData[] ContactName;
                public InstanceData[] Message;
                public InstanceData[] EmailSubject;
                public InstanceData[] number;
                public InstanceData[] ordinal;
                public InstanceData[] FoodOfGrocery;
                public InstanceData[] TaskContent_Any;
                public InstanceData[] Message_Any;
                public InstanceData[] EmailSubject_Any;
                public InstanceData[] SearchTexts_Any;
            }
            [JsonProperty("$instance")]
            public _Instance _instance;
        }
        public _Entities Entities;

        [JsonExtensionData(ReadData = true, WriteData = true)]
        public IDictionary<string, object> Properties {get; set; }

        public void Convert(dynamic result)
        {
            var app = JsonConvert.DeserializeObject<DispatchLuis>(JsonConvert.SerializeObject(result));
            Text = app.Text;
            AlteredText = app.AlteredText;
            Intents = app.Intents;
            Entities = app.Entities;
            Properties = app.Properties;
        }

        public (Intent intent, double score) TopIntent()
        {
            Intent maxIntent = Intent.None;
            var max = 0.0;
            foreach (var entry in Intents)
            {
                if (entry.Value.Score > max)
                {
                    maxIntent = entry.Key;
                    max = entry.Value.Score.Value;
                }
            }
            return (maxIntent, max);
        }
    }
}