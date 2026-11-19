using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Xml;

namespace ClementiaPet
{
    // DataContractJsonSerializer populates the content fields when loading the embedded resource.
#pragma warning disable 0649
    [DataContract]
    internal sealed class SpeechLine
    {
        [DataMember] public string id, category, text, next, basis;
        [DataMember] public string[] forms;
    }

    [DataContract]
    internal sealed class ChatOption
    {
        [DataMember] public string label, reply, next, action;
        [DataMember] public string[] words;
        internal bool Matches(string input)
        {
            return MatchesWord(label, input) || words.Any(w => MatchesWord(w, input));
        }
        private static bool MatchesWord(string word, string input)
        {
            return input == word || (word.Length > 1 && input.Contains(word));
        }
    }

    [DataContract]
    internal sealed class ChatNode
    {
        [DataMember] public string id, prompt;
        [DataMember] public ChatOption[] options;
    }

    [DataContract]
    internal sealed class DialogueFile
    {
        [DataMember] public SpeechLine[] lines;
        [DataMember] public ChatNode[] nodes;
        [DataMember] public string fallback;
        [DataMember] public string[] goodbyes;
    }

#pragma warning restore 0649
    internal sealed class Dialogue
    {
        private readonly Dictionary<string,string> labels = new Dictionary<string,string>();
        internal readonly Dictionary<string, ChatNode> Nodes = new Dictionary<string, ChatNode>();
        internal readonly List<SpeechLine> Lines = new List<SpeechLine>();
        internal readonly string Fallback;
        internal readonly string[] Goodbyes;
        internal static Dialogue Load()
        {
            var data = new DialogueFile();
            var labels = new Dictionary<string,string>();
            try
            {
                using (Stream stream = typeof(Dialogue).Assembly.GetManifestResourceStream("conversation.zh-CN.json"))
                {
                    if (stream == null) return new Dialogue(data);
                    using (var reader = JsonReaderWriterFactory.CreateJsonReader(stream, XmlDictionaryReaderQuotas.Max))
                    {
                        var document = new XmlDocument();
                        document.Load(reader);
                        foreach (XmlNode label in document.SelectNodes("/root/ui/*"))
                            if (!string.IsNullOrWhiteSpace(label.InnerText)) labels[label.LocalName] = label.InnerText;
                        data.lines = ReadItems<SpeechLine>(document,"/root/lines/item");
                        data.nodes = ReadItems<ChatNode>(document,"/root/nodes/item");
                        data.goodbyes = ReadItems<string>(document,"/root/goodbyes/item");
                        XmlNode fallback = document.SelectSingleNode("/root/fallback");
                        if (fallback != null) data.fallback = fallback.InnerText;
                    }
                }
            }
            catch (XmlException) { }
            var dialogue = new Dialogue(data);
            foreach (var label in labels) dialogue.labels.Add(label.Key, label.Value);
            return dialogue;
        }

        internal string Label(string key)
        {
            string value;
            return labels.TryGetValue(key, out value) ? value : key;
        }

        private static T[] ReadItems<T>(XmlDocument document, string path)
        {
            var entries = new List<T>();
            var serializer = new DataContractJsonSerializer(typeof(T));
            foreach (XmlNode item in document.SelectNodes(path))
            {
                try
                {
                    using (var reader = new XmlNodeReader(item))
                        entries.Add((T)serializer.ReadObject(reader, false));
                }
                catch (SerializationException) { }
                catch (XmlException) { }
            }
            return entries.ToArray();
        }
        internal Dialogue(DialogueFile data)
        {
            Fallback = data.fallback;
            Goodbyes = (data.goodbyes ?? new string[0]).Where(w => !string.IsNullOrWhiteSpace(w)).ToArray();
            foreach (ChatNode node in data.nodes ?? new ChatNode[0])
                if (node != null && !string.IsNullOrWhiteSpace(node.id) && !string.IsNullOrWhiteSpace(node.prompt)
                    && !Nodes.ContainsKey(node.id)) Nodes.Add(node.id, node);
            foreach (ChatNode node in Nodes.Values)
                node.options = (node.options ?? new ChatOption[0]).Where(o => o != null &&
                    !string.IsNullOrWhiteSpace(o.label) && !string.IsNullOrWhiteSpace(o.reply) &&
                    o.next != null && Nodes.ContainsKey(o.next) && o.words != null &&
                    o.words.All(w => !string.IsNullOrWhiteSpace(w)) &&
                    (string.IsNullOrEmpty(o.action) || o.action == "drank" || o.action == "off" ||
                    o.action == "snooze" || o.action == "dismiss")).ToArray();
            var ids = new HashSet<string>();
            foreach (SpeechLine line in data.lines ?? new SpeechLine[0])
                if (line != null && !string.IsNullOrWhiteSpace(line.id) && !string.IsNullOrWhiteSpace(line.text)
                    && !string.IsNullOrWhiteSpace(line.basis) && line.forms != null &&
                    line.forms.Any(f => f == "human" || f == "ray") &&
                    (line.category == "evening" || line.category == "water" || line.category == "idle") &&
                    (string.IsNullOrEmpty(line.next) || Nodes.ContainsKey(line.next)) && ids.Add(line.id))
                    Lines.Add(line);
        }
    }

    internal sealed class ChatSession
    {
        private readonly Dialogue dialogue;
        private readonly Func<string,bool> apply;
        private bool waterInvitation;
        internal ChatNode Node { get; private set; }
        internal bool Closed { get; private set; }

        internal ChatSession(Dialogue dialogue, string start, Func<string,bool> apply, bool waterInvitation = false)
        {
            this.dialogue = dialogue;
            this.apply = apply;
            this.waterInvitation = waterInvitation || start == "water";
            Node = dialogue.Nodes[start];
        }

        internal string Send(string input)
        {
            if (Closed || string.IsNullOrWhiteSpace(input)) return null;
            input = input.Trim().TrimEnd('。','？','！','?','!');
            ChatOption option = Node.options.FirstOrDefault(o => o.Matches(input));
            if (dialogue.Goodbyes.Any(input.Contains)) { Closed = true; return null; }
            if (option == null && waterInvitation && dialogue.Nodes.ContainsKey("water"))
                option = dialogue.Nodes["water"].options.FirstOrDefault(o => o.Matches(input));
            if (option == null) option = dialogue.Nodes["home"].options.FirstOrDefault(o => o.Matches(input));
            if (option == null)
            {
                Node = dialogue.Nodes["home"];
                return dialogue.Fallback;
            }
            Node = dialogue.Nodes[option.next];
            if (!string.IsNullOrEmpty(option.action))
            {
                waterInvitation = false;
                if (!apply(option.action)) return Node.prompt;
            }
            return option.reply;
        }
    }
}
