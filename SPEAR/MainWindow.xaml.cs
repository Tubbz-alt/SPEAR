using SPEAR.Models;
using SPEAR.Models.Devices;
using SPEAR.Parsers;
using SPEAR.Parsers.Devices;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SPEAR
{
    public partial class MainWindow : Window, IFileParserCallback
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Properties
        /////////////////////////////////////////////////////////////////////////////////////////
        public static string ArchiveName;

        public static DeviceInfo DeviceSelected;
        public static FileParser DeviceFileParser;

        public static string directoryOfFiles;
        private static string percentFormat = "{0}%";

        /////////////////////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////////////////////
        public MainWindow()
        {
            InitializeComponent();

            // Initialize MainWindow
            Initialize();
        }


        /////////////////////////////////////////////////////////////////////////////////////////
        // Methods
        /////////////////////////////////////////////////////////////////////////////////////////
        public void Initialize()
        {
            ArchiveName = Path.Combine(Path.GetTempPath(), Process.GetCurrentProcess().Id.ToString());

            // Add detectors to ComboBox
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "AISense", Tag = new AISense() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "Arktis P2000", Tag = new ArktisP2000() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "AtomTex", Tag = new AtomTex() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "AtomTex AT6101C", Tag = new AtomTexAT6101C() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "AtomTex AT6103", Tag = new AtomTexAT6103() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "BNC SAM 950", Tag = new BNCSam() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "BubbleTech FlexSpec", Tag = new BubbleTechFlexSpec() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "Detective X", Tag = new DetectiveX() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "Flir identiFINDER", Tag = new IdentiFINDER() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "Flir R400", Tag = new FlirR400() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "Flir R500", Tag = new FlirR500() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "RadEye SPRD-GN", Tag = new RadEyeSprdGn() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "H3DA400", Tag = new H3DA400() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "Kromek D3S (DHS)", Tag = new KromekD3SDhs() }); 
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "Kromek D3S (NSDD)", Tag = new KromekD3SNsdd() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "Mirion Spirident Mobile", Tag = new MirionSpirdentMobile() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "Mirion SpirPack", Tag = new MirionSpirPack() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "NucSafe Guardian", Tag = new NucSafeGuardian() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "NucTech", Tag = new NucTech() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "Nuvia RadScout", Tag = new NuviaRadScout() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "Nuvia Siris", Tag = new NuviaSiris() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "Ortec Detective Remote", Tag = new OrtecDetectiveRemote() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "Polimaster", Tag = new Polimaster() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "PSI PERM", Tag = new PsiPerm() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "RadEagle", Tag = new RadEagle() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "RadEye SPRD", Tag = new RadEyeSPRD() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "RadSeeker", Tag = new RadSeeker() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "RIID Eye X", Tag = new RIIDEyeX() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "RS 350", Tag = new Rs350() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "RS 700", Tag = new Rs700() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "RSI SR-10", Tag = new RSI() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "Symetrica Discover Mobile", Tag = new SymetricaDiscoverMobile() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "Symetrica SN33-N", Tag = new SymetricaSN33N() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "Thermo RadHalo", Tag = new ThermoRadHalo() });
            ComboBox_DetectorType.Items.Add(new ComboBoxItem() { Content = "Verifinder", Tag = new Verifinder() });

            ComboBox_FileType.Visibility = Visibility.Collapsed;

            // Setup last saved settings
            TextBox_DirectoryPath.Text = (string)Properties.Settings.Default["DirectoryPath"];
            int deviceSelectedIndex = (int)Properties.Settings.Default["DeviceSelectedIndex"];
            if (deviceSelectedIndex == -1) {
                DeviceSelected = null;
            }
            else {
                ComboBox_DetectorType.SelectedIndex = deviceSelectedIndex;
            }
        }



        /////////////////////////////////////////////////////////////////////////////////////////
        // Events
        /////////////////////////////////////////////////////////////////////////////////////////
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // Save user settings for later
            Properties.Settings.Default["DirectoryPath"] = TextBox_DirectoryPath.Text;
            Properties.Settings.Default["DeviceSelectedIndex"] = ComboBox_DetectorType.SelectedIndex;
            Properties.Settings.Default.Save();
        }

        private void DetectorType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Start off by disabling both TextBlock and ComboBox
            TextBlock_FileType.Visibility = Visibility.Collapsed;
            ComboBox_FileType.Visibility = Visibility.Collapsed;
            DeviceSelected = (DeviceInfo)((ComboBoxItem)ComboBox_DetectorType.SelectedItem).Tag;
            
            // Check how many file extensions are supported
            if (DeviceSelected.SupportedFileExts.Count == 1) {
                TextBlock_FileType.Text = DeviceSelected.SupportedFileExts[0].FileExtName;
                TextBlock_FileType.Tag = DeviceSelected.SupportedFileExts[0];
                TextBlock_FileType.Visibility = Visibility.Visible;
            }
            else if (DeviceSelected.SupportedFileExts.Count > 1) {
                ComboBox_FileType.ItemsSource = DeviceSelected.SupportedFileExts
                    .Select(f => new ComboBoxItem() {
                        Content = f.FileExtName,
                        Tag = f
                    });
                ComboBox_FileType.Visibility = Visibility.Visible;
                ComboBox_FileType.SelectedIndex = 0;
            }
            else {
                return;
            }
        }

        private void Parse_Click(object sender, RoutedEventArgs e)
        {
            // Save user settings for later
            Properties.Settings.Default["DirectoryPath"] = TextBox_DirectoryPath.Text;
            Properties.Settings.Default["DeviceSelectedIndex"] = ComboBox_DetectorType.SelectedIndex;
            Properties.Settings.Default.Save();

            // Check directory is valid
            if (TextBox_DirectoryPath.Text == string.Empty) {
                MessageBox.Show("You have not specified a directory.", "No Directory Entered");
                return;
            }
            else if (Directory.Exists(TextBox_DirectoryPath.Text) == false) {
                MessageBox.Show("The directory entered does not exist.", "Directory Doesn't Exist");
                return;
            }
            directoryOfFiles = TextBox_DirectoryPath.Text;

            // Check detector dropdown is selected
            if (ComboBox_DetectorType.SelectedIndex == -1) {
                MessageBox.Show("A detector has not been selected from the dropdown list.", "Select Detector");
                return;
            }

            // Check file type dropdown is selected
            FileExt fileExt = null;
            if (ComboBox_FileType.Visibility == Visibility.Visible) {
                if (ComboBox_FileType.SelectedItem == null) {
                    MessageBox.Show("A file type has not been selected from the dropdown list.", "Select File Type");
                    return;
                }
                fileExt = (FileExt)((ComboBoxItem)ComboBox_FileType.SelectedItem).Tag;
            }
            else {
                fileExt = (FileExt)(TextBlock_FileType.Tag);
            }
            
            // Get FileParser selected
            DeviceFileParser = fileExt.FileParser;

            // Register callbacks
            DeviceFileParser.RegisterCallback(this);

            // Get all files from directory
            var allFilePaths = DeviceFileParser.GetAllFilePaths(directoryOfFiles);
            if (allFilePaths.Count() == 0) {
                MessageBox.Show("There were no " + fileExt.FileExtName + " files found found in the directory given.", "No Files Found");
                return;
            }

            // Initialize file parser with files
            DeviceFileParser.InitializeFilePaths(allFilePaths);

            // Parse files
            Thread parseThread = new Thread(new ThreadStart(DeviceFileParser.Parse));
            parseThread.Start();
        }

        private void UserGuide_Click(object sender, RoutedEventArgs e)
        {
            string userGuidePath = Directory.GetCurrentDirectory();
            userGuidePath = Path.Combine(userGuidePath, "UserGuides");
            userGuidePath = Path.Combine(userGuidePath, "SPEAR-UserGuide.pdf");

            Process.Start(userGuidePath);
        }

        public void ParsingError(string title, string message)
        {
            MessageBox.Show(message, title);
        }

        public void ParsingStarted()
        {
            LoadingSpinner.StartText(string.Format(percentFormat, 0));
        }

        public void ParsingUpdate(float percentComplete)
        {
            LoadingSpinner.StartText(string.Format(percentFormat, (int)(percentComplete * 100)));
        }

        public void ParsingComplete(IEnumerable<DeviceData> deviceDatas)
        {
            // Check if any files were parsed
            if (deviceDatas.Count() == 0) {
                MessageBox.Show("There where no files that parsed or parsed correctly. No export file was created.", "Parsing Failed");
                LoadingSpinner.StopText();
                return;
            }

            // Export to CSV file on desktop
            string filePath = Path.Combine(directoryOfFiles, Path.ChangeExtension(DeviceFileParser.FileName, ".csv"));
            CSVExporter.ExportFiles(deviceDatas, filePath);

            MessageBox.Show("Parseing complete. An excel spreadsheet and zipped folder with files used can be found in the same directory with the name of the device.", "Parsing Complete");
            
            LoadingSpinner.StopText();
        }
    }
}
