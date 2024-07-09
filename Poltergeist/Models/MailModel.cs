using ProtoBuf;
using System;

namespace Poltergeist.models
{
    [ProtoContract]
    public class MailModel
    {
        [ProtoMember(1)]
        public string From { get; set; }

        [ProtoMember(2)]
        public string To { get; set; }

        [ProtoMember(3)]
        public string cc { get; set; }

        [ProtoMember(4)]
        public string Subject { get; set; }

        [ProtoMember(5)]
        public string Content { get; set; }

        [ProtoMember(6)]
        public string ShortContent { get; set; }

        [ProtoMember(7)]
        public DateTime Time { get; set; }

        [ProtoMember(8)]
        public string Date { get; set; }

        [ProtoMember(9)]
        public string HtmlBody { get; set; }

        [ProtoMember(10)]
        public bool IsHtml { get; set; }

        [ProtoMember(11)]
        public uint uid { get; set; }
    }
}
