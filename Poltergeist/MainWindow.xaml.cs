
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
using Microsoft.UI;
using Microsoft.UI.Windowing;
using System.Text;
using System.Security.Cryptography;
using Windows.Security.Credentials;
using Microsoft.Extensions.Configuration;
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
        public IConfigurationRoot app_secrets; 

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
            LoadIcon(@"Assets/LogoGhostT.ico");
            Title = "Poltergeist";
            populateNav();

        }

        private void LoadIcon(string iconName)
        {
            IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.SetIcon(iconName);
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
            //setup secrets
            app_secrets = new ConfigurationBuilder().AddUserSecrets<MainWindow>().Build();
            
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

            //TODO: Run this on first open
            //gen_store_key();
            LoadAccounts();


            //loadAccountPublics();
            //loadAccountPrivates();


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

        //account store logic
        private string GetOrCreateKey()
        {
            var vault = new PasswordVault();
            try
            {
                // Try to retrieve the existing key
                var credential = vault.Retrieve(_appName, "Poltergeist_Cypher");
                credential.RetrievePassword();
                return credential.Password;
            }
            catch (Exception)
            {
                // Key doesn't exist, generate a new one
                string newKey = gen_key();
                vault.Add(new PasswordCredential(_appName, "Poltergeist_Cypher", newKey));
                return newKey;
            }
        }

        private string gen_key()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var keyBytes = new byte[32]; // 256 bits
                rng.GetBytes(keyBytes);
                return Convert.ToBase64String(keyBytes);
            }
        }

        //TODO: implement en-/decrypt of accounts using key from vault

        private void StoreAccounts()
        {
            try
            {
                string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string folderPath = Path.Combine(roaming, "Poltergeist");
                string cachePath = Path.Combine(folderPath, "accounts.dat");

                byte[] serializedData;

                using (var stream = new MemoryStream())
                {
                    Serializer.Serialize(stream, _accounts);
                    serializedData = stream.ToArray();
                }


                string encryptionKey = GetOrCreateKey();
                byte[] encryptedData = Encrypt(serializedData, encryptionKey);

                File.WriteAllBytes(cachePath, encryptedData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error storing accounts: {ex.Message}");
            }
        }


        private void LoadAccounts()
        {
            try
            {
                string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string folderPath = Path.Combine(roaming, "Poltergeist");
                string cachePath = Path.Combine(folderPath, "accounts.dat");
                if (File.Exists(cachePath))
                {
                    byte[] encryptedData = File.ReadAllBytes(cachePath);
                    string encryptionKey = GetOrCreateKey();
                    byte[] decryptedData = Decrypt(encryptedData, encryptionKey);
                    using (var stream = new MemoryStream(decryptedData))
                    {
                        _accounts = Serializer.Deserialize<ObservableCollection<AccountsModel>>(stream);
                    }
                }
                else
                {
                    _accounts = new ObservableCollection<AccountsModel>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading accounts: {ex.Message}");
                _accounts = new ObservableCollection<AccountsModel>();
            }
        }

        private byte[] Encrypt(byte[] plainBytes, string key)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = Convert.FromBase64String(key);
                aes.IV = new byte[16];

                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        cs.Write(plainBytes, 0, plainBytes.Length);
                    }
                    return ms.ToArray();
                }
            }
        }

        private byte[] Decrypt(byte[] cipherBytes, string key)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = Convert.FromBase64String(key);
                aes.IV = new byte[16];

                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream(cipherBytes))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var resultMs = new MemoryStream())
                {
                    cs.CopyTo(resultMs);
                    return resultMs.ToArray();
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
            s.Children.Add(new TextBlock { Text = "Use Oauth: (Microsoft / Google)" });
            s.Children.Add(OauthCheck);

            var OauthDropDown = new ComboBox();
            OauthDropDown.Items.Add("Microsoft");
            OauthDropDown.Items.Add("Google");
            s.Children.Add(new TextBlock { Text = "Oauth Platform:" });
            s.Children.Add(OauthDropDown);


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
                ac.OauthPlatform = OauthDropDown.SelectedIndex;
                ac.security = (SecureSocketOptions)SecDropDown.SelectedIndex;



                _accounts.Add(ac);

                //storeAccountPublics();
                //storeAccountPrivates();
                StoreAccounts();

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
