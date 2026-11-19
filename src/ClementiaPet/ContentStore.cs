using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace ClementiaPet
{
    [DataContract]
    internal sealed class BubbleContent
    {
        [DataMember(Name = "key")] public string Key { get; set; }
        [DataMember(Name = "lines")] public List<string> Lines { get; set; }
    }

    [DataContract]
    internal sealed class DocumentContent
    {
        [DataMember(Name = "id")] public string Id { get; set; }
        [DataMember(Name = "title")] public string Title { get; set; }
        [DataMember(Name = "body")] public string Body { get; set; }
    }

    [DataContract]
    internal sealed class ContentFile
    {
        [DataMember(Name = "schemaVersion")] public int SchemaVersion { get; set; }
        [DataMember(Name = "firstRunHint")] public string FirstRunHint { get; set; }
        [DataMember(Name = "bubbles")] public List<BubbleContent> Bubbles { get; set; }
        [DataMember(Name = "documents")] public List<DocumentContent> Documents { get; set; }
    }

    internal sealed class ContentStore
    {
        private readonly Dictionary<string, List<string>> bubbles = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, int> bubbleCursor = new Dictionary<string, int>();
        private readonly List<DocumentContent> documents = new List<DocumentContent>();

        internal string FirstRunHint { get; private set; }
        internal int DocumentCount { get { return documents.Count; } }

        internal static ContentStore Load()
        {
            var result = new ContentStore();
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("zh-CN.json"))
                {
                    if (stream == null) throw new InvalidDataException("Missing embedded content/zh-CN.json.");
                    var serializer = new DataContractJsonSerializer(typeof(ContentFile));
                    var data = (ContentFile)serializer.ReadObject(stream);
                    result.Read(data);
                }
            }
            catch
            {
                // Content shipping is part of the product. Keep a readable fallback so a
                // damaged optional resource never leaves the pet in an unusable state.
                result.ReadFallback();
            }
            return result;
        }

        private void Read(ContentFile data)
        {
            if (data == null || data.SchemaVersion != 1) throw new InvalidDataException("Unsupported content schema.");
            if (data.Bubbles == null || data.Documents == null) throw new InvalidDataException("Incomplete content.");
            FirstRunHint = data.FirstRunHint;
            foreach (BubbleContent entry in data.Bubbles)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Key) || entry.Lines == null || entry.Lines.Count == 0)
                    throw new InvalidDataException("Invalid bubble content.");
                bubbles[entry.Key] = entry.Lines;
            }
            foreach (DocumentContent document in data.Documents)
            {
                if (document == null || string.IsNullOrEmpty(document.Id) ||
                    string.IsNullOrEmpty(document.Title) || string.IsNullOrEmpty(document.Body))
                    throw new InvalidDataException("Invalid document content.");
                documents.Add(document);
            }
            if (documents.Count == 0 || string.IsNullOrEmpty(FirstRunHint))
                throw new InvalidDataException("Content is empty.");
        }

        private void ReadFallback()
        {
            FirstRunHint = "点手稿读信，双击变身，右键找我";
            AddBubble("human.head", "博士，我在听。您可以慢慢说。", "嗯，我记下您刚才那半句了。");
            AddBubble("human.hand", "这次会面，要从哪件小事谈起？", "手边的事先放下也没关系。");
            AddBubble("human.body", "我在。您想先处理哪一件事？", "今天的水流很安静，适合把话说完。");
            AddBubble("human.manuscript", "这页写的是……请您慢慢读。", "纸角我替您压住了。");
            AddBubble("ray.head", "这股水流，往您那边去了。", "这里的声音，我听见了。");
            AddBubble("ray.body", "收到。我会在这一带巡视。", "航线没有偏，我还在这一带。");
            AddDocument("shore-spelling", "《“岸”的另一种写法》",
                "博士，假如让海里的人给“岸”下定义，您猜他们先画哪一边？\n\n我画了潮水退去后还留在石头上的那条细线。\n\n您可以在旁边再画一条。");
            AddDocument("five-minutes", "《会面前五分钟》",
                "会面表上空出了五分钟。\n\n我原想用来复核措辞，可对面那位研究员也带了同一本小说。\n\n等我们争完第三章的结尾，茶刚好凉到能喝。");
            AddDocument("helper-name", "《小帮手的名字》",
                "小帮手已经绕桌子游了三圈。\n\n我叫它回来，它没回头；我试着叫它“逗号”，它倒停在了句子中间。\n\n博士，这算它自己选的吗？");
        }

        private void AddBubble(string key, params string[] lines)
        {
            bubbles.Add(key, new List<string>(lines));
        }

        private void AddDocument(string id, string title, string body)
        {
            documents.Add(new DocumentContent { Id = id, Title = title, Body = body });
        }

        internal string NextBubble(string key)
        {
            List<string> lines;
            if (!bubbles.TryGetValue(key, out lines) || lines.Count == 0) return string.Empty;
            int index = 0;
            if (bubbleCursor.TryGetValue(key, out index))
            {
                index = (index + 1) % lines.Count;
                bubbleCursor[key] = index;
            }
            else bubbleCursor[key] = 0;
            return lines[index];
        }

        internal int IndexOfDocument(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                for (int i = 0; i < documents.Count; ++i)
                    if (string.Equals(documents[i].Id, id, StringComparison.Ordinal)) return i;
            }
            return 0;
        }

        internal DocumentContent DocumentAt(int index)
        {
            if (documents.Count == 0) return null;
            index = ((index % documents.Count) + documents.Count) % documents.Count;
            return documents[index];
        }
    }
}
