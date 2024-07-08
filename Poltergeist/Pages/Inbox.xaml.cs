using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MailKit;
using Microsoft.Identity.Client;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Poltergeist.models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Collections.ObjectModel;
using ProtoBuf;
using Microsoft.UI.Xaml.Documents;
using System.Threading.Tasks;
using MimeKit.Cryptography;
using MimeKit;
using MailKit.Net.Smtp;
using Google.Apis.Auth.OAuth2;
using System.Threading;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Poltergeist.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Inbox : Page
    {
        //mail stuff (Per Account)
        AccountsModel curAcc = null;
        private ObservableCollection<MailModel> _inbox { get; set; }
        private List<uint> _inboxIds { get; set; }
        private DateTime newest;

        //auth
        private IPublicClientApplication _msftOauthApp { get; set; }
        private IAccount _msftOauthAcc { get; set; }
        private AuthenticationResult _msftOauthRes { get; set; }

        //states
        private bool init = true;
        private bool edit = false;
        private bool web = false;

        private bool leftToEdit = false;
        private bool sendFlyOut = false;

        public Inbox()
        {
            this.InitializeComponent();
        }
        // Data handling (IMAP)


        private void loadCache(string user)
        {
            try
            {
                string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string folderPath = Path.Combine(roaming, "Poltergeist");
                string cachePath = Path.Combine(folderPath, user + "_cache.dat");
                string idCachePath = Path.Combine(folderPath, user + "_cache.ids");
                string timePath = Path.Combine(folderPath, user + "lastUpdate");
                List<MailModel> cachedMessages = new List<MailModel>();

                // Deserialize MimeMessages from the local cache file
                if (File.Exists(cachePath) && File.Exists(timePath) && File.Exists(idCachePath))
                {
                    using (FileStream fs = new FileStream(cachePath, FileMode.Open))
                    {
                        cachedMessages = Serializer.Deserialize<List<MailModel>>(fs);
                    }
                    using (FileStream fs = new FileStream(timePath, FileMode.Open))
                    {
                        newest = Serializer.Deserialize<DateTime>(fs);
                    }
                    using (FileStream fs = new FileStream(idCachePath, FileMode.Open))
                    {
                        _inboxIds = Serializer.Deserialize<List<uint>>(fs);
                    }
                }

                _inbox = new ObservableCollection<MailModel>(cachedMessages);
                
            }
            catch (Exception)
            {
                
            }
        }

        private void storeCache(string user)
        {
            try
            {
                string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string folderPath = Path.Combine(roaming, "Poltergeist");

                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }
                string cachePath = Path.Combine(folderPath, user + "_cache.dat");
                string idCachePath = Path.Combine(folderPath, user + "_cache.ids");
                string timePath = Path.Combine(folderPath, user + "lastUpdate");

                using (var fs = File.Open(cachePath, FileMode.Create, FileAccess.ReadWrite))
                {
                    if (_inbox.Count < 500)
                    {
                        Serializer.Serialize(fs, _inbox);
                    }
                    else
                    {
                        var tmp_inbox = _inbox.Take(500).ToList();
                        Serializer.Serialize(fs, tmp_inbox);
                    }
                }
                using (var fs = File.Open(idCachePath, FileMode.Create, FileAccess.ReadWrite))
                {
                    if (_inbox.Count < 500)
                    {
                        Serializer.Serialize(fs, _inboxIds);
                    }
                    else
                    {
                        var tmp_inboxIds = _inboxIds.Take(500).ToList();
                        Serializer.Serialize(fs, tmp_inboxIds);
                    }
                }
                using (var fs = File.Open(timePath, FileMode.Create, FileAccess.ReadWrite))
                {
                    Serializer.Serialize(fs, _inbox.ElementAt(0).Time);
                }
            }
            catch (Exception) { }
            
        }

        public async Task pullMail(AccountsModel acc)
        {
            if (acc.pulling) { return; }
            var currentWindow = (Application.Current as App).m_window;

            var client = new ImapClient();

            await client.ConnectAsync(acc.ImapHost, acc.ImapPort, acc.security);

            if (acc.Oauth2)
            {
                switch (acc.OauthPlatform)
                {
                    case 0: //msft
                        var scopes = new string[] {
                            "email",
                            "offline_access",
                            "https://outlook.office.com/IMAP.AccessAsUser.All", // Only needed for IMAP
                            //"https://outlook.office.com/POP.AccessAsUser.All",  // Only needed for POP
                            "https://outlook.office.com/SMTP.Send", // Only needed for SMTP
                        };
                        if (_msftOauthApp == default(IPublicClientApplication))
                        {
                            var options = new PublicClientApplicationOptions
                            {
                                ClientId = currentWindow.app_secrets["msft_client_id"],
                                RedirectUri = "https://login.microsoftonline.com/common/oauth2/nativeclient",
                                
                            };

                            _msftOauthApp = PublicClientApplicationBuilder
                                .CreateWithApplicationOptions(options)
                                .Build();
                            Helpers.TokenCacheHelper.EnableSerialization(_msftOauthApp.UserTokenCache);

                            _msftOauthAcc = (await _msftOauthApp.GetAccountsAsync()).FirstOrDefault();
                        }
                        try
                        {
                            _msftOauthRes = await _msftOauthApp.AcquireTokenSilent(scopes, _msftOauthAcc)
                                              .ExecuteAsync();
                        }
                        catch (MsalUiRequiredException)
                        {
                            try
                            {
                                _msftOauthRes = await _msftOauthApp.AcquireTokenInteractive(scopes).ExecuteAsync();
                            }
                            catch (Exception ex) 
                            {
                                currentWindow.logToFile(ex.ToString());
                            }
                        }
                        var oauth2 = new SaslMechanismOAuth2(_msftOauthRes.Account.Username, _msftOauthRes.AccessToken);
                        await client.AuthenticateAsync(oauth2);
                        break;
                    case 1: //google
                        var credentials = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                            new ClientSecrets
                            {
                                ClientId = currentWindow.app_secrets["google_client_id"],
                                ClientSecret = currentWindow.app_secrets["google_client_secret"]
                            },
                            new[] { "https://mail.google.com/" },
                            "user",
                            CancellationToken.None);
                        var oauth2_goog = new SaslMechanismOAuth2(acc.User, credentials.Token.AccessToken);
                        await client.AuthenticateAsync(oauth2_goog);
                        break;
                }

            }
            else
            {
                await client.AuthenticateAsync(acc.User, acc.Password);
            }


            await client.Inbox.OpenAsync(FolderAccess.ReadOnly);
            IList<UniqueId> revUids;
            if (newest != default(DateTime))
            {
                newest = newest.AddMinutes(1);
                revUids = await client.Inbox.SearchAsync(SearchQuery.DeliveredAfter(newest));
            }
            else
            {
                revUids = await client.Inbox.SearchAsync(SearchQuery.All);
            }
            var uids = revUids.Reverse();


            var count = 0;
            foreach (var uid in uids)
            {
                count = count + 1;
                var msg = await client.Inbox.GetMessageAsync(uid);
                var mail = new MailModel();
                
                mail.Subject = msg.Subject;
                mail.From = msg.From.ToString();
                mail.To = msg.To.ToString();
                mail.cc = msg.Cc.ToString();
                mail.Content = msg.TextBody ?? msg.HtmlBody;
                mail.Time = msg.Date.DateTime;
                mail.Date = msg.Date.ToString().Substring(0, 10);

                mail.HtmlBody = msg.HtmlBody;

                if (mail.HtmlBody == null)
                {
                    mail.IsHtml = false;
                }
                else
                {
                    mail.IsHtml = true;
                }
                
                if (msg.TextBody != null)
                {
                    try
                    {
                        mail.ShortContent = msg.TextBody.Replace("\n", " ").Substring(0, 50).Trim() + "...";
                    }
                    catch (Exception)
                    {
                        mail.ShortContent = msg.TextBody.Trim();
                    }
                }
                if (msg.Body is MultipartEncrypted)
                {
                    mail.ShortContent = "Encrypted";
                    mail.Content = "Encrypted\nPoltergeist does not currently support encryption";
                }
                if (newest != default(DateTime))
                {
                    if (!_inboxIds.Contains(uid.Id))
                    {
                        _inbox.Insert(count - 1, mail);
                        _inboxIds.Insert(count - 1, uid.Id);
                    }
                }
                else
                {
                    _inbox.Add(mail);
                    _inboxIds.Add(uid.Id);
                }

                if (count > 1000)
                {
                    break;
                }
                if (uids.Count() > 500)
                {
                    if (currentWindow != null)
                    {
                        currentWindow.Title = "Poltergeist | " + count + "/1000";
                    }
                }
                else
                {
                    if (currentWindow != null)
                    {
                        currentWindow.Title = "Poltergeist | " + count + "/" + uids.Count();
                    }
                }
            }

            await client.DisconnectAsync(true);
            storeCache(acc.User);
            acc.pulling = false;
            //write cache
            if (currentWindow != null)
            {
                currentWindow.Title = "Poltergeist";
            }
        }

        private async void mailOpened(object sender, RoutedEventArgs e)
        {
            if (sender is Button clButton)
            {
                
                if (clButton.DataContext is MailModel openedMail)
                {
                    From_Box.Text = openedMail.From;
                    To_Box.Text = openedMail.To;
                    CC_Box.Text = openedMail.cc;
                    Subject_Box.Text = openedMail.Subject; 
                    if (openedMail.IsHtml)
                    {
                        try
                        {
                            SwapToWebView();
                            await MessageWebDisplay.EnsureCoreWebView2Async();
                            string darkModeStyles = @"
                                                        <style>
                                                            body {
                                                                background-color: #363062;
                                                                color: #FFFFFF;
                                                            }
                                                            /* Add more styles as needed */
                                                        </style>
                                                    ";
                            MessageWebDisplay.NavigateToString(darkModeStyles + openedMail.HtmlBody);
                            MessageWebDisplay.Width = grid.ActualWidth - 450;
                            MessageWebDisplay.Height = grid.ActualHeight - 20;
                        }
                        catch (Exception exception)
                        {
                            var currentWindow = (Application.Current as App).m_window;
                            currentWindow.logToFile(exception.ToString());

                            // Display error to user
                            MessageDisplay.Visibility = Visibility.Visible;
                            MessageWebDisplay.Visibility = Visibility.Collapsed;
                            Paragraph paragraph = new Paragraph();
                            Run run = new Run();
                            run.Text = "Error loading HTML content: " + exception.Message;
                            paragraph.Inlines.Add(run);
                            MessageDisplay.Blocks.Add(paragraph);
                        }
                    }
                    else
                    {
                        SwapToTextView();
                        //MessageDisplay.Text = openedMail.Content;
                        MessageDisplay.Blocks.Clear();
                        Paragraph paragraph = new Paragraph();
                        Run run = new Run();
                        run.Text = openedMail.Content;

                        paragraph.Inlines.Add(run);


                        MessageDisplay.Blocks.Add(paragraph);
                        MessageDisplay.MaxWidth = grid.ActualWidth - 450;
                    }

                }
            }
        }

        // Data handling (SMTP)
        private MimeMessage buildMsg()
        {
            var mail = new MimeMessage();

            mail.From.Add(MailboxAddress.Parse(From_Box.Text));
            mail.To.Add(MailboxAddress.Parse(To_Box.Text));
            if (CC_Box.Text != "")
            {
                mail.Cc.Add(MailboxAddress.Parse(CC_Box.Text));
            }
            mail.Subject = Subject_Box.Text;

            BodyBuilder mailBody = new BodyBuilder();
            string contentString = string.Empty;
            MessageWrite.Document.GetText(Microsoft.UI.Text.TextGetOptions.AdjustCrlf, out contentString);
            mailBody.TextBody = contentString;
            mail.Body = mailBody.ToMessageBody();

            return mail;
        }
        
        private async void sendMail(AccountsModel acc)
        {
            var currentWindow = (Application.Current as App).m_window;
            var client = new SmtpClient();

            await client.ConnectAsync(acc.SmtpHost, acc.SmtpPort, /*acc.security*/ SecureSocketOptions.Auto);

            if (acc.Oauth2)
            {
                switch (acc.OauthPlatform)
                {
                    case 0: //msft
                        var scopes = new string[] {
                            "email",
                            "offline_access",
                            "https://outlook.office.com/IMAP.AccessAsUser.All", // Only needed for IMAP
                            //"https://outlook.office.com/POP.AccessAsUser.All",  // Only needed for POP
                            "https://outlook.office.com/SMTP.Send", // Only needed for SMTP
                        };
                        if (_msftOauthApp == default(IPublicClientApplication))
                        {
                            var options = new PublicClientApplicationOptions
                            {
                                ClientId = currentWindow.app_secrets["msft_client_id"],
                                RedirectUri = "https://login.microsoftonline.com/common/oauth2/nativeclient",

                            };

                            _msftOauthApp = PublicClientApplicationBuilder
                                .CreateWithApplicationOptions(options)
                                .Build();
                            Helpers.TokenCacheHelper.EnableSerialization(_msftOauthApp.UserTokenCache);

                            _msftOauthAcc = (await _msftOauthApp.GetAccountsAsync()).FirstOrDefault();
                        }
                        try
                        {
                            _msftOauthRes = await _msftOauthApp.AcquireTokenSilent(scopes, _msftOauthAcc)
                                              .ExecuteAsync();
                        }
                        catch (MsalUiRequiredException)
                        {
                            try
                            {
                                _msftOauthRes = await _msftOauthApp.AcquireTokenInteractive(scopes).ExecuteAsync();
                            }
                            catch { }
                        }
                        var oauth2_mic = new SaslMechanismOAuth2(_msftOauthRes.Account.Username, _msftOauthRes.AccessToken);
                        await client.AuthenticateAsync(oauth2_mic);
                        break;
                    case 1: //google
                        var credentials = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                            new ClientSecrets
                            {
                                ClientId = currentWindow.app_secrets["google_client_id"],
                                ClientSecret = currentWindow.app_secrets["google_client_secret"]
                            },
                            new[] {"https://mail.google.com/"},
                            "user",
                            CancellationToken.None);
                        var oauth2_goog = new SaslMechanismOAuth2(acc.User, credentials.Token.AccessToken);
                        await client.AuthenticateAsync(oauth2_goog);
                        break;
                }

            }
            else
            {
                await client.AuthenticateAsync(acc.User, acc.Password);
            }

            await client.SendAsync(buildMsg());
            client.Disconnect(true);
        }


        //state management
        private void SwapToWebView()
        {
            hideWrite();

            MessageDisplay.Visibility = Visibility.Collapsed;
            MessageWebDisplay.Visibility = Visibility.Visible;

            MessageWebDisplay.Width = grid.ActualWidth - 450;
            MessageWebDisplay.Height = grid.ActualHeight - 20;

            web = true;
        }

        private void SwapToTextView()
        {
            hideWrite();
            
            MessageDisplay.Visibility = Visibility.Visible;
            MessageWebDisplay.Visibility = Visibility.Collapsed;

            MessageDisplay.MaxWidth = grid.ActualWidth - 450;

            web = false;
        }

        private void hideWrite()
        {
            MessageWrite.Visibility = Visibility.Collapsed;
            From_Box.IsReadOnly = true;
            To_Box.IsReadOnly = true;
            CC_Box.IsReadOnly = true;
            Subject_Box.IsReadOnly = true;
            writeBtn.Visibility = Visibility.Visible;
            sendBtn.Visibility = Visibility.Collapsed;
            edit = false;
        }

        private void SwapToWriteView()
        {
            
            MessageWrite.Visibility = Visibility.Visible;
            MessageDisplay.Visibility = Visibility.Collapsed;
            MessageWebDisplay.Visibility = Visibility.Collapsed;

            //headers
            From_Box.IsReadOnly = false;
            To_Box.IsReadOnly = false;
            CC_Box.IsReadOnly = false;
            Subject_Box.IsReadOnly = false;

            //Populate with defaults
            From_Box.Text = curAcc.mail;
            To_Box.Text = "";
            CC_Box.Text = "";
            Subject_Box.Text = "";
            MessageWrite.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, "");

            //sizing
            MessageWrite.MaxWidth = grid.ActualWidth - 450;
            MessageWrite.Height = grid.ActualHeight - 20;
            MessageWrite.MaxHeight = grid.ActualHeight - 20;

            //button
            writeBtn.Visibility = Visibility.Collapsed;
            sendBtn.Visibility = Visibility.Visible;

            //states
            web = false;
            edit = true;

        }

        // ui handlers
        private void Frame_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            resize();
        }

        private void resize()
        {
            if (init)
            {
                init = false;
                return;
            }
            if (web)
            {
                MessageWebDisplay.Width = grid.ActualWidth - 450;
                MessageWebDisplay.Height = grid.ActualHeight - 20;
            }
            else if (edit)
            {
                MessageWrite.MaxWidth = grid.ActualWidth - 450;
                MessageWrite.Height = grid.ActualHeight - 20;
                MessageWrite.MaxHeight = grid.ActualHeight - 20;

            }
            else { MessageDisplay.MaxWidth = grid.ActualWidth - 450; }


            // header
            HeaderBar.Width = grid.ActualWidth - 450;

            From_Box.Width = grid.ActualWidth - 450 - 10;
            To_Box.Width = (grid.ActualWidth - 450) / 2 - 10;
            CC_Box.Width = (grid.ActualWidth - 450) / 2 - 10;
            Subject_Box.Width = grid.ActualWidth - 450 - 10;
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            
            _inbox = new ObservableCollection<MailModel>();
            _inboxIds = new List<uint>();
            var payload = e.Parameter as AccountsModel;
            curAcc = payload;
            try
            {
                loadCache(payload.User);
                await pullMail(payload);
                if (grid.ActualWidth > 450)
                {
                    HeaderBar.Width = grid.ActualWidth - 450;
                }
                else
                {
                    HeaderBar.Width = 900;
                }
                resize();
            }
            catch(Exception ex)
            {
                var currentWindow = (Application.Current as App).m_window;
                currentWindow.logToFile(ex.Message);
            }
            //base.OnNavigatedTo(e);
        }

        private void writeBtn_Click(object sender, RoutedEventArgs e)
        {
            SwapToWriteView();
        }

        private void sendBtn_Click(object sender, RoutedEventArgs e)
        {
            sendMail(curAcc);
        }

        private void sendBtn_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            writeBtn.Visibility = Visibility.Visible;
            writeBtn.Margin = new Thickness(0, 0, 30, 75);
            writeBtnRect.Height = 15;
            writeBtnRect.Width = 15;
            sendFlyOut = true;
        }

        private async void sendBtn_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            await Task.Delay(10);
            if (sendFlyOut && !leftToEdit)
            {
                writeBtn.Visibility = Visibility.Collapsed;
                writeBtn.Margin = new Thickness(0, 0, 30, 30);
                writeBtnRect.Height = 50;
                writeBtnRect.Width = 50;
                sendFlyOut = false;
                leftToEdit = false;
            }
        }

        private void writeBtn_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sendFlyOut && edit && leftToEdit)
            {
                writeBtn.Visibility = Visibility.Collapsed;
                writeBtn.Margin = new Thickness(0, 0, 30, 30);
                writeBtnRect.Height = 50;
                writeBtnRect.Width = 50;
                sendFlyOut = false;
                leftToEdit = false;
            }
        }

        private void writeBtn_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sendFlyOut)
            {
                leftToEdit = true;
            }
        }
    }
}
