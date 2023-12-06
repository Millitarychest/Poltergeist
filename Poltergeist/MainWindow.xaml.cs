
using Microsoft.UI.Xaml;
using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Controls;
using System.IO;
using ProtoBuf;
using Poltergeist.models;
using MailKit.Security;
using Poltergeist.Pages;
using System.Runtime.InteropServices;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Poltergeist
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        

        //accounts (Per profile if i do those)
        private ObservableCollection<AccountsModel> _accounts { get; set; }
        private string _appName = "Poltergeist";


        //internal states

        private int selectedAccount;


        public MainWindow()
        {
            selectedAccount = 0;
            setup();
            //populate 
            this.InitializeComponent();
            uiSetup();
        }


        //inits

        private void uiSetup()
        {
            Title = "Poltergeist";
            populateNav();

        }

        private void populateNav()
        {
            NavBar.MenuItems.Clear();
            var count = 0;
            foreach (var account in _accounts)
            {
                if (count == selectedAccount) {
                    var item = new NavigationViewItem
                    {
                        DataContext = account,
                        Content = account.User,
                        Icon = new SymbolIcon(Symbol.Account),
                        IsSelected = true
                    };
                    NavBar.MenuItems.Add(item);
                }
                else
                {
                    var item = new NavigationViewItem
                    {
                        DataContext = account,
                        Content = account.User,
                        Icon = new SymbolIcon(Symbol.Account)
                    };
                    NavBar.MenuItems.Add(item);
                }

                count = count++;
            }
        }


        private void setup()
        {
            //setup paths
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folderPath = Path.Combine(roaming, "Poltergeist");
            if (!Directory.Exists(folderPath)) 
            { 
                Directory.CreateDirectory(folderPath);
            }

            //inits
            _accounts = new ObservableCollection<AccountsModel>();

            //account inti (temp)

            loadAccountPublics();
            loadAccountPrivates();


            //storeAccountPublics();
            //storeAccountPrivates();

        }

        public void logToFile(string log)
        {
            try
            {
                string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string folderPath = Path.Combine(roaming, "Poltergeist");
                string cachePath = Path.Combine(folderPath, "logs.log");
                StreamWriter s = File.AppendText(cachePath);
                s.WriteLine(log);
                s.Close();
            }
            catch { }
        }

        //account store
        //Publics (no passwords)
        private void storeAccountPublics() 
        {
            try
            {
                string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string folderPath = Path.Combine(roaming, "Poltergeist");
                string cachePath = Path.Combine(folderPath, "accounts.dat");
                using (var fs = File.Open(cachePath, FileMode.Create, FileAccess.ReadWrite))
                {
                    Serializer.Serialize(fs, _accounts);
                }
            }
            catch { }
        }

        private void loadAccountPublics() 
        {
            try
            {
                string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string folderPath = Path.Combine(roaming, "Poltergeist");
                string cachePath = Path.Combine(folderPath, "accounts.dat");

                if (File.Exists(cachePath))
                {
                    using (var fs = File.Open(cachePath, FileMode.Open, FileAccess.Read))
                    {
                        _accounts = Serializer.Deserialize<ObservableCollection<AccountsModel>>(fs);
                    }
                }
            }
            catch { }
        }

        //privates (Passwords)

        private void storeAccountPrivates() // only if Account not oauth and only on add (for edit remove old instance)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var vault = new Windows.Security.Credentials.PasswordVault();
                foreach (var account in _accounts)
                {
                    if (!account.Oauth2)
                    {
                        vault.Add(new Windows.Security.Credentials.PasswordCredential(_appName, account.User + ":" + account.ImapHost, account.Password));
                    }
                }
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private void loadAccountPrivates()
        {
            if(_accounts.Count > 0)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var vault = new Windows.Security.Credentials.PasswordVault();
                    foreach(var account in _accounts)
                    {
                        if (!account.Oauth2)
                        {
                            try
                            {
                                var cred = vault.Retrieve(_appName, account.User + ":" + account.ImapHost);
                                account.Password = cred.Password;
                            }
                            catch (Exception )
                            {
                            }
                        }
                    }

                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }

        //dialogs
        private async void newMAilDialog()
        {
            var s = new StackPanel();
            var a = new StackPanel { Orientation = Orientation.Horizontal };
            var b = new StackPanel { Orientation = Orientation.Horizontal };

            //Imap
            a.Children.Add(new TextBlock { Text = "Imap Host:", MinWidth=200, MaxWidth=200});
            a.Children.Add(new TextBlock { Text = "Port:", HorizontalAlignment= HorizontalAlignment.Right});

            var Imaphost = new TextBox { MinWidth = 200, MaxWidth = 200, AcceptsReturn = false };
            var Imapport = new TextBox { HorizontalAlignment = HorizontalAlignment.Right, AcceptsReturn = false };
            b.Children.Add(Imaphost);
            b.Children.Add(Imapport);

            s.Children.Add(a);
            s.Children.Add(b);

            //smtp
            var c = new StackPanel { Orientation = Orientation.Horizontal };
            var d = new StackPanel { Orientation = Orientation.Horizontal };

            c.Children.Add(new TextBlock { Text = "Smtp Host:", MinWidth = 200, MaxWidth = 200 });
            c.Children.Add(new TextBlock { Text = "Port:", HorizontalAlignment = HorizontalAlignment.Right });

            var Smtphost = new TextBox { MinWidth = 200, MaxWidth = 200, AcceptsReturn = false };
            var Smtpport = new TextBox { HorizontalAlignment = HorizontalAlignment.Right, AcceptsReturn = false };
            d.Children.Add(Smtphost);
            d.Children.Add(Smtpport);

            s.Children.Add(c);
            s.Children.Add(d);

            //details
            var user = new TextBox { AcceptsReturn = false };
            var pw = new TextBox { AcceptsReturn = false };
            s.Children.Add(new TextBlock { Text = "User / Email:" });
            s.Children.Add(user);
            s.Children.Add(new TextBlock { Text = "Password:" });
            s.Children.Add(pw);
            var mail = new TextBox { AcceptsReturn = false };
            s.Children.Add(new TextBlock { Text = "Dispatch Email:" });
            s.Children.Add(mail);

            var SecDropDown = new ComboBox();
            SecDropDown.Items.Add("None");
            SecDropDown.Items.Add("Auto");
            SecDropDown.Items.Add("SslOnConnect");
            SecDropDown.Items.Add("StartTls");
            s.Children.Add(new TextBlock { Text = "Security:" });
            s.Children.Add(SecDropDown);

            var OauthCheck = new CheckBox();
            s.Children.Add(new TextBlock { Text = "Use Oauth:" });
            s.Children.Add(OauthCheck);


            ContentDialog newMAilDialog = new ContentDialog
            {
                Title = "New Email Account",
                Content = s,
                CloseButtonText = "Cancel",
                PrimaryButtonText = "Add"
            };
            newMAilDialog.XamlRoot = frame.XamlRoot;

            ContentDialogResult res = await newMAilDialog.ShowAsync();

            if(res is ContentDialogResult.Primary)
            {
                var ac = new AccountsModel();
                ac.ImapPort = int.Parse(Imapport.Text);
                ac.ImapHost = Imaphost.Text;
                ac.User = user.Text;
                ac.Password = pw.Text;
                ac.SmtpPort = int.Parse(Smtpport.Text);
                ac.SmtpHost = Smtphost.Text;
                ac.mail = mail.Text;
                ac.Oauth2 = (bool)OauthCheck.IsChecked;
                ac.security = (SecureSocketOptions)SecDropDown.SelectedIndex;



                _accounts.Add(ac);

                storeAccountPublics();
                storeAccountPrivates();

                uiSetup();
            }
        }


        //handlers


        private void NavBar_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var item = (NavigationViewItem)args.SelectedItem;
            if (item.DataContext is AccountsModel acc)
            {
                frame.Navigate(typeof(Inbox), acc);
            }
            else if ((string)item.Content == "Add New")
            {
                // call add dialog
                if (_accounts.Count < 19)
                {
                    newMAilDialog();
                }
            }
        }

        

    }
}
