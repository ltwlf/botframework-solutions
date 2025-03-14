using Newtonsoft.Json;
using System.Collections.Generic;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.AI.Luis;
namespace Luis
{
    public class emailLuis: IRecognizerConvert
    {
        public string Text;
        public string AlteredText;
        public enum Intent {
            AddFlag, 
            AddMore, 
            CancelMessages, 
            CheckMessages, 
            ConfirmMessages, 
            Delete, 
            Forward, 
            None, 
            QueryLastText, 
            ReadAloud, 
            Reply, 
            SearchMessages, 
            SendEmail, 
            ShowNext, 
            ShowPrevious
        };
        public Dictionary<Intent, IntentScore> Intents;

        public class _Entities
        {
            // Simple entities
            public string[] OrderReference;
            public string[] SenderName;
            public string[] Category;
            public string[] ContactName;
            public string[] Attachment;
            public string[] Message;
            public string[] RelationshipName;
            public string[] Time;
            public string[] Line;
            public string[] PositionReference;
            public string[] Date;
            public string[] FromRelationshipName;
            public string[] EmailSubject;
            public string[] SearchTexts;

            // Built-in entities
            public string[] email;
            public double[] ordinal;

            // Pattern.any
            public string[] Message_Any;
            public string[] EmailSubject_Any;
            public string[] SearchTexts_Any;

            // Instance
            public class _Instance
            {
                public InstanceData[] OrderReference;
                public InstanceData[] SenderName;
                public InstanceData[] Category;
                public InstanceData[] ContactName;
                public InstanceData[] Attachment;
                public InstanceData[] Message;
                public InstanceData[] RelationshipName;
                public InstanceData[] Time;
                public InstanceData[] Line;
                public InstanceData[] PositionReference;
                public InstanceData[] Date;
                public InstanceData[] FromRelationshipName;
                public InstanceData[] EmailSubject;
                public InstanceData[] SearchTexts;
                public InstanceData[] email;
                public InstanceData[] ordinal;
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
            var app = JsonConvert.DeserializeObject<emailLuis>(JsonConvert.SerializeObject(result));
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
