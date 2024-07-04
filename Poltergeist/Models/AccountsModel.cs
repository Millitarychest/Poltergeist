using MailKit.Security;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Poltergeist.models
{
    [ProtoContract]
    public class AccountsModel
    {
        [ProtoMember(1)]
        public string ImapHost { get; set; }

        [ProtoMember(2)]
        public int ImapPort { get; set; }

        [ProtoMember(4)]
        public string Password { get; set; }

        [ProtoMember(5)]
        public string User { get; set; }

        [ProtoMember(6)]
        public SecureSocketOptions security { get; set; }

        [ProtoMember(7)]
        public bool Oauth2 { get; set; }

        [ProtoMember(8)]
        public int OauthPlatform { get; set; } // 0 - Microsoft | 1 - Google 

        public bool pulling { get; set; }

        [ProtoMember(9)]
        public string SmtpHost { get; set; }

        [ProtoMember(10)]
        public int SmtpPort { get; set; }

        [ProtoMember(11)]
        public string mail { get; set; }
    }
}
