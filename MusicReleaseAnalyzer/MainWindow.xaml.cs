using HtmlAgilityPack;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using ITunesLibraryParser;
using System.Text.RegularExpressions;

namespace MusicReleaseAnalyzer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // The source of the CancellationToken that will be used to cancel async tasks
        private CancellationTokenSource _tokenSource;

        // Elements used to handle column click header
        private GridViewColumnHeader _lastHeaderClicked = null;
        private ListSortDirection _lastDirection = ListSortDirection.Ascending;

        // Releases collection
        public ObservableCollection<Release> Releases { get; set; }

        // Discarded collection
        public ObservableCollection<Release> Discarded { get; set; }

        // Contains the urls of the album art to download
        private Dictionary<string, string> _coverUrls = new Dictionary<string, string>();

        // Contains a list of patterns used to split a string
        private readonly string[] _separator = new string[] { ", ", "/", " & ", "&", " featuring ", " ft ", " ft. ", " feat. ", " Feat. ", " ft.", " feat.", " feat ", " ft", " Ft ", " vs ", " x ", " X " };

        // Contains a list of Regex patterns
        private readonly List<string> _regexPatterns = new List<string>()
        {
            @"(?<track>\d+)\.\s(?<artist>.+)\s-\s(?<title>.+)\s(?<length>\d+:\d+)\s/(?<bpm>\d+.+)\s/(?<key>.+)", // track. artist - title len:ght /123bpm /key
            @"(?<track>\d+)\.\s(?<artist>.+)\s-\s(?<title>.+)\s(?<length>\d+:\d+)\s(?<bpm>\d+.+)\s(?<key>.+)", // track. artist - title len:ght 123bpm key
            @"(?<track>\d+)\.\s(?<artist>.+)\s-\s(?<title>.+)\s(?<length>\d+:\d+)", // track. artist - title len:ght
            @"(?<track>\d+)\.\s(?<artist>.+)\s-\s(?<title>.+)\s(?<label>\[.+\])", // track. artist - title [label]
            @"(?<track>\d+)\.\s(?<artist>.+)\s-\s(?<title>.+)\s(?<length>\(\d+:\d+\))", // track. artist - title (len:ght)
            @"(?<track>\d+)\.\s(?<artist>.+)\s-\s(?<title>.+)", // track. artist - title
            @"(?<artist>.+)\s-\s(?<title>.+)\s(?<length>\d+:\d+)\s/(?<bpm>\d+.+)\s/(?<key>.+)", // artist - title len:ght /123bpm /key
            @"(?<artist>.+)\s-\s(?<title>.+)\s(?<length>\d+:\d+)\s(?<bpm>\d+.+)\s(?<key>.+)", // artist - title len:ght 123bpm key
            @"(?<time>\[.+\])\s(?<artist>.+)\s-\s(?<title>.+)\s(?<label>\[.+\])", // [time] artist - title [label]
            @"(?<time>\[.+\])\s(?<artist>.+)\s-\s(?<title>.+)", // [time] artist - title
            @"(?<artist>.+)\s-\s(?<title>.+)\s(?<label>\[.+\])", // artist - title [label]
            @"(?<artist>.+)\s-\s(?<title>.+)\s(?<length>\(\d+:\d+\))", // artist - title (len:ght)
            @"(?<artist>.+)\s-\s(?<title>.+)\s(?<length>\d+:\d+)", // artist - title len:ght
            @"(?<artist>.+)\s-\s(?<title>.+)", // artist - title
            @"(?<track>\d+)\.\s(?<artist>.+)\s–\s(?<title>.+)\s(?<length>\d+:\d+)\s/(?<bpm>\d+.+)\s/(?<key>.+)", // track. artist – title len:ght /123bpm /key
            @"(?<track>\d+)\.\s(?<artist>.+)\s–\s(?<title>.+)\s(?<length>\d+:\d+)\s(?<bpm>\d+.+)\s(?<key>.+)", // track. artist – title len:ght 123bpm key
            @"(?<track>\d+)\.\s(?<artist>.+)\s–\s(?<title>.+)\s(?<length>\d+:\d+)", // track. artist – title len:ght
            @"(?<track>\d+)\.\s(?<artist>.+)\s–\s(?<title>.+)\s(?<label>\[.+\])", // track. artist – title [label]
            @"(?<track>\d+)\.\s(?<artist>.+)\s–\s(?<title>.+)(?<length>\(\d+:\d+\))", // track. artist – title (len:ght)
            @"(?<track>\d+)\.\s(?<artist>.+)\s–\s(?<title>.+)", // track. artist – title
            @"(?<artist>.+)\s–\s(?<title>.+)\s(?<length>\d+:\d+)\s/(?<bpm>\d+.+)\s/(?<key>.+)", // artist – title len:ght /123bpm /key
            @"(?<artist>.+)\s–\s(?<title>.+)\s(?<length>\d+:\d+)\s(?<bpm>\d+.+)\s(?<key>.+)", // artist – title len:ght 123bpm key
            @"(?<time>\[.+\])\s(?<artist>.+)\s–\s(?<title>.+)\s(?<label>\[.+\])", // [time] artist – title [label]
            @"(?<time>\[.+\])\s(?<artist>.+)\s–\s(?<title>.+)", // [time] artist – title
            @"(?<artist>.+)\s–\s(?<title>.+)\s(?<label>\[.+\])", // artist – title [label]
            @"(?<artist>.+)\s–\s(?<title>.+)\s(?<length>\(\d+:\d+\))", // artist – title (len:ght)
            @"(?<artist>.+)\s–\s(?<title>.+)\s(?<length>\d+:\d+)", // artist – title len:ght         
            @"(?<artist>.+)\s–\s(?<title>.+)", // artist – title
        };

        public MainWindow()
        {
            InitializeComponent();

            // Initialize the collections
            Releases = new ObservableCollection<Release>();
            Discarded = new ObservableCollection<Release>();
        }

        /// <summary>
        /// Get the <see cref="CancellationToken"/> from the default <see cref="CancellationTokenSource"/>
        /// </summary>
        /// <returns>The System.Threading.CancellationToken</returns>
        private CancellationToken GetCancellationToken()
        {
            _tokenSource = new CancellationTokenSource();
            return _tokenSource.Token;
        }

        private async void chooseLinkFileButton_Click(object sender, RoutedEventArgs e)
        {
            // Prompts the user a dialog to choose the file
            // that contains the links
            var dialog = new OpenFileDialog();
            var result = dialog.ShowDialog();

            // Check that the user has correctly selected a file
            if (!result.HasValue || !result.Value) return;

            // Retrieve the file's path
            selectedLinkFileTextBlock.Text = dialog.FileName;

            // Get the cancellation token
            var token = GetCancellationToken();

            try
            {
                // Read the links from the file and than gets and filters the releases
                await GetAndFilterReleasesAsync(await ReadDataFromFileAsync(selectedLinkFileTextBlock.Text, token), token);
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Task cancelled", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (WebException)
            {
                MessageBox.Show("Task cancelled", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // Set the data context and show the releases
            DataContext = Releases;
            await Dispatcher.InvokeAsync(() => { releasesListView.ItemsSource = Releases; });

            // Final UI touches
            await CleanupLayout();

            // Download the album artworks previously added to the list
            await Dispatcher.InvokeAsync(DownloadCover);
        }

        /// <summary>
        /// Downloads and filters the releases that are found in the list of links given.
        /// </summary>
        /// <param name="links">The list of links to parse.</param>
        /// <param name="token">The <see cref="CancellationToken"/> used to notify for cancellation.</param>
        private async Task GetAndFilterReleasesAsync(IReadOnlyList<string> links, CancellationToken token)
        {
            // Get the UI ready
            await PrepareLayout(links.Count, "Parsing link 1 of " + links.Count.ToString());

            // Get the releases from the links
            Releases = await GetReleasesAsync(links, token);

            // Get the UI ready
            await PrepareLayout(Releases.Count, "Filtering release 1 of " + Releases.Count.ToString());

            // Get a list of filtered releases
            Releases = await FilterReleasesAsync(Releases, token);
        }

        /// <summary>
        /// Gets a list of the releases from the list of links given
        /// </summary>
        /// <param name="links">The list of links to parse.</param>
        /// <param name="token">The <see cref="CancellationToken"/> used to notify for cancellation.</param>
        /// <returns>A System.Collections.ObjectModel.ObservableCollection that contains the releases.</returns>
        private async Task<ObservableCollection<Release>> GetReleasesAsync(IReadOnlyList<string> links, CancellationToken token)
        {
            // Initialize the collection
            var releases = new ObservableCollection<Release>();

            // Start the async task
            await Task.Run(async () =>
            {
                // Iterate through the list
                for (int i = 1; i <= links.Count; i++)
                {
                    // Cancel the async task if required
                    if (token.IsCancellationRequested) break;

                    // Update the UI with the current progress
                    await UpdateLayout(i, "Parsing link " + i.ToString() + " of " + links.Count.ToString());

                    // Check for valid link before getting data
                    if (string.IsNullOrWhiteSpace(links[i - 1])) continue;
                    if (!Uri.TryCreate(links[i - 1], UriKind.Absolute, out Uri uriResult)) continue;
                    if (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps) continue;

                    // Download the data from the link, parse it, 
                    // and add the release to the list.
                    releases.Add(await ParseLinkAsync(links[i - 1], token));
                }
            }, token);

            return releases;
        }

        /// <summary>
        /// Get the release data from the given link.
        /// </summary>
        /// <param name="link">The link to parse.</param>
        /// <param name="token">The <see cref="CancellationToken"/> used to notify for cancellation.</param>
        /// <returns>A MusicReleaseAnalyzer.Release</returns>
        private async Task<Release> ParseLinkAsync(string link, CancellationToken token)
        {
            // Download the webpage and load it to the HtmlDocument
            var document = new HtmlDocument();
            using (var client = new WebClient())
            {
                token.Register(client.CancelAsync);
                document.Load(await client.OpenReadTaskAsync(link), Encoding.UTF8);
            }

            // Get a list of the HtmlNodes that holds the releases
            var parentNode = document.DocumentNode.SelectNodes("//div[@class='base fullstory']").FirstOrDefault();

            // Initialize properties
            string date = string.Empty, title = string.Empty, genre = string.Empty, label = string.Empty, artist = string.Empty, coverLink = string.Empty;
            var hasErrors = false;
            var subParentNodeList = new List<HtmlNode>();

            try
            {
                // Date
                date = System.Web.HttpUtility.HtmlDecode(parentNode?.SelectNodes("//div[@class='short-info']").FirstOrDefault()?.SelectNodes("//span[@class='icon icon-calendar']").FirstOrDefault()?.InnerText);

                // Cover link
                coverLink = "http://techdeephouse.com" + parentNode?.SelectNodes("//div[@class='poster']").FirstOrDefault()?.Descendants("img").FirstOrDefault()?.Attributes["src"].Value;

                // Get a list of the child nodes that hold the release properties
                subParentNodeList = parentNode?.SelectNodes("//div[@class='maincont']").FirstOrDefault()?.SelectNodes("//div[@style='word-spacing: 1.1px;']").ToList();

                // Title
                title = System.Web.HttpUtility.HtmlDecode(subParentNodeList?.FirstOrDefault(x => x.InnerText.Contains("Title: "))?.InnerText.Remove(0, 7));

                // Genre
                genre = System.Web.HttpUtility.HtmlDecode(subParentNodeList?.FirstOrDefault(x => x.InnerText.Contains("Genre: "))?.InnerText.Remove(0, 7));

                // Label
                label = System.Web.HttpUtility.HtmlDecode(subParentNodeList?.FirstOrDefault(x => x.InnerText.Contains("Label: "))?.InnerText.Remove(0, 7));

                // Artist
                artist = subParentNodeList?.FirstOrDefault(x => x.InnerText.Contains("Artist: "))?.InnerText;
                artist = System.Web.HttpUtility.HtmlDecode(artist?.Substring(artist.IndexOf("Artist: ") + 8));
            }
            catch (Exception)
            {
                // Notify if any error was encountered while parsing the release properties
                hasErrors = true;
            }

            // Initialize the list that will hold the songs
            var songs = new List<string>();
            try
            {
                // Get the songs
                songs.AddRange(from node in subParentNodeList
                               where node.FirstChild.Name.Equals("b")
                               where !string.IsNullOrWhiteSpace(node.FirstChild.InnerText) 
                               && !node.FirstChild.InnerText.Contains("Artist:") 
                               && !node.FirstChild.InnerText.Contains("Title:") 
                               && !node.FirstChild.InnerText.Contains("Genre:") 
                               && !node.FirstChild.InnerText.Contains("Label:") 
                               && !node.FirstChild.InnerText.Contains("Quality:")
                               select System.Web.HttpUtility.HtmlDecode(node.FirstChild.InnerText));
            }
            catch (Exception)
            {
                // If any error was encountered while getting the songs,
                // notify it and return an empty list
                songs = new List<string>();
                hasErrors = true;
            }

            // Initialize element with default BitmapImage.
            // The original cover will be downloaded async later.
            var cover = new BitmapImage();
            cover.Freeze();

            // If the cover link is valid, add it to the list
            // so we'll able to download it later
            if (!coverLink.StartsWith("http")) coverLink = "";
            if (!Uri.TryCreate(coverLink, UriKind.Absolute, out Uri uriResult)) coverLink = "";
            if (uriResult != null)
            {
                if (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps) coverLink = "";
            }
            if (!string.IsNullOrWhiteSpace(coverLink)) _coverUrls.Add(link, coverLink);

            return new Release(artist, genre, date, title, label, link, songs, cover, hasErrors);
        }

        private async Task<ObservableCollection<Release>> FilterReleasesAsync(IReadOnlyList<Release> collection, CancellationToken token)
        {
            var labels = await ReadDataFromFileAsync(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location) + @"\labels.txt", token);
            var artists = await GetArtistsFinalList(await ReadDataFromFileAsync(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location) + @"\artists.txt", token), token);

            var releases = new ObservableCollection<Release>();
            await Task.Run(async () =>
            {
                for (int i = 1; i <= collection.Count; i++)
                {
                    if (token.IsCancellationRequested) break;

                    await UpdateLayout(i, "Filtering release " + i.ToString() + " of " + collection.Count.ToString());

                    if (collection[i - 1].HasErrors || string.IsNullOrWhiteSpace(collection[i - 1].Label) || string.IsNullOrWhiteSpace(collection[i - 1].Artist))
                    {
                        releases.Add(collection[i - 1]);
                        continue;
                    }

                    if (!labels.Any(collection[i - 1].Label.Contains))
                    {
                        var releaseArtist = new List<string>() { collection[i - 1].Artist };

                        if (_separator.Any(collection[i - 1].Artist.Contains))
                        {
                            releaseArtist.AddRange(collection[i - 1].Artist.Split(_separator, StringSplitOptions.RemoveEmptyEntries).ToList());
                        }

                        if (releaseArtist.Intersect(artists).Any())
                        {
                            releases.Add(collection[i - 1]);
                        }
                        else
                        {
                            Discarded.Add(collection[i - 1]);
                        }
                    }
                    else
                    {
                        releases.Add(collection[i - 1]);
                    }
                }
            }, token);

            return releases;
        }

        /// <summary>
        /// Asynchronously read the content of a file line by line.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <param name="token">The <see cref="CancellationToken"/> used to notify for cancellation.</param>
        /// <returns>A <see cref="List{T}"/> containing the lines read.</returns>
        private static async Task<List<string>> ReadDataFromFileAsync(string path, CancellationToken token)
        {
            // Initialize the list that will be returned
            var data = new List<string>();

            // Initialize the reader
            using (var reader = new StreamReader(path))
            {
                // Iterate through the stream
                while (!reader.EndOfStream)
                {
                    // Cancel the async task if required
                    if (token.IsCancellationRequested) break;

                    // Asynchronously read a line and add it to the list 
                    data.Add(await reader.ReadLineAsync());
                }
            }

            return data;
        }

        /// <summary>
        /// Asynchronously split the artist string if it actually contains many artists' names
        /// </summary>
        /// <param name="artists">The collection that contains the artists.</param>
        /// <param name="token">The <see cref="CancellationToken"/> used to notify for cancellation.</param>
        /// <returns>A <see cref="List{T}"/> containing the splitted artists.</returns>
        private async Task<List<string>> GetArtistsFinalList(IEnumerable<string> artists, CancellationToken token)
        {
            // Initialize the list that will be returned
            var finalArtists = new List<string>();

            // Start the async task
            await Task.Run(() =>
            {
                // Iterate through the artists
                foreach (string artist in artists)
                {
                    // Cancel the async task if required
                    if (token.IsCancellationRequested) break;

                    // If the artist string is composed by multiple single artists,
                    // then split it and add each of them to the list.
                    // Otherwise just add the artist as is.
                    if (_separator.Any(artist.Contains))
                    {
                        finalArtists.AddRange(artist.Split(_separator, StringSplitOptions.RemoveEmptyEntries).ToList());
                    }
                    else
                    {
                        finalArtists.Add(artist);
                    }
                }
            }, token);

            return finalArtists;
        }

        /// <summary>
        /// Download a cover from the list asynchronously.
        /// </summary>
        private void DownloadCover()
        {
            // Quit if there's no cover to download
            if (!_coverUrls.Any()) return;

            // Initialize the webclient
            using (var client = new WebClient() { Encoding = Encoding.UTF8 })
            {
                // Register the event fired when the download is completed
                client.DownloadDataCompleted += Client_DownloadDataCompleted;

                // Start the async download
                client.DownloadDataAsync(new Uri(_coverUrls.FirstOrDefault().Value));
            }
        }

        /// <summary>
        /// Executes when an async download is completed from the calling <see cref="WebClient"/>
        /// </summary>
        /// <param name="sender">Contains a reference to the object that raised the event.</param>
        /// <param name="e">Provides data for the <see cref="WebClient.DownloadDataCompleted"/> event.</param>
        private async void Client_DownloadDataCompleted(object sender, DownloadDataCompletedEventArgs e)
        {
            // Check if the download encountered errors
            if (e.Error != null)
            {
                // Notify that the release has error because we couldn't download it's cover.
                // If we can't find the realease in the Releases collection,
                // it means that it's in the Discarded.
                if (Releases.Any(x => x.Link == _coverUrls.FirstOrDefault().Key))
                {
                    Releases.First(x => x.Link == _coverUrls.FirstOrDefault().Key).HasErrors = true;
                }
                else
                {
                    Discarded.First(x => x.Link == _coverUrls.FirstOrDefault().Key).HasErrors = true;
                }
            }

            // Ensure that the download wasn't cancelled and there wasn't any error
            if (e.Error == null && !e.Cancelled)
            {
                // Add the cover to it's correspondind release.
                // If we can't find the realease in the Releases collection,
                // it means that it's in the Discarded.
                if (Releases.Any(x => x.Link == _coverUrls.FirstOrDefault().Key))
                {
                    Releases.First(x => x.Link == _coverUrls.FirstOrDefault().Key).Cover = GetCoverArt(e.Result);
                }
                else
                {
                    Discarded.First(x => x.Link == _coverUrls.FirstOrDefault().Key).Cover = GetCoverArt(e.Result);
                }
            }

            // Remove the cover's link we've just downloaded from the list
            _coverUrls.Remove(_coverUrls.FirstOrDefault().Key);

            // Recursively call the async download
            await Task.Run(() => { DownloadCover(); });
        }

        /// <summary>
        /// Converts an array of byte into its BitmapImage representation.
        /// </summary>
        /// <param name="data">The byte array to convert.</param>
        /// <returns>A System.Windows.Media.Imaging.BitmapImage</returns>
        private BitmapImage GetCoverArt(byte[] data)
        {
            // Initialize the BitmapImage
            var bitmap = new BitmapImage();
            try
            {
                // Initialize the stream
                MemoryStream ms = new MemoryStream(data);
                ms.Seek(0, SeekOrigin.Begin);

                // Set the stream as the BitmapImage's source 
                bitmap.BeginInit();
                bitmap.StreamSource = ms;
                bitmap.EndInit();
            }
            catch (Exception)
            {
                bitmap = new BitmapImage();
            }

            // Finalize the BitmapImage. This operation is necessary otherwise a DependencyObject exception
            // will be thrown when setting the ListView's ItemSource.
            bitmap.Freeze();

            return bitmap;
        }

        /// <summary>
        /// Prepare the UI for a long running task.
        /// </summary>
        /// <param name="maxValue">The maximum value of the <see cref="ProgressBar"/>.</param>
        /// <param name="status">The text that displays the status of the task.</param>
        private async Task PrepareLayout(double maxValue, string status)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                statusProgressBar.Visibility = Visibility.Visible;
                statusProgressBar.Maximum = maxValue;
                mainStatusBarItem.Content = status;
                cancelButton.Visibility = Visibility.Visible;
                chooseLinkFileButton.IsEnabled = false;
                discardedButton.IsEnabled = false;
                dateChooseButton.IsEnabled = false;
                compareItunesButton.IsEnabled = false;
                CompareWithListMenuItem.IsEnabled = false;
                CompareWithBoth.IsEnabled = false;
                quickSearchButton.IsEnabled = false;
            });
        }

        /// <summary>
        /// Restore the UI after a long running task.
        /// </summary>
        /// <returns></returns>
        private async Task CleanupLayout()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                statusProgressBar.Visibility = Visibility.Collapsed;
                statusProgressBar.Value = 0;
                mainStatusBarItem.Content = "Ready";
                cancelButton.Visibility = Visibility.Collapsed;
                chooseLinkFileButton.IsEnabled = true;
                discardedButton.IsEnabled = true;
                dateChooseButton.IsEnabled = true;
                compareItunesButton.IsEnabled = true;
                CompareWithListMenuItem.IsEnabled = true;
                CompareWithBoth.IsEnabled = true;
                quickSearchButton.IsEnabled = true;
                _tokenSource = null;
            });
        }

        /// <summary>
        /// Update the Ui with the current progress.
        /// </summary>
        /// <param name="value">The current value of the <see cref="ProgressBar"/>.</param>
        /// <param name="status">The text that displays the current status of the task.</param>
        private async Task UpdateLayout(int value, string status)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                statusProgressBar.Value = value;
                mainStatusBarItem.Content = status;
            });
        }

        /// <summary>
        /// Handles the event that is fired when a column header is clicked.
        /// </summary>
        /// <param name="sender">Contains a reference to the object that raised the event.</param>
        /// <param name="e">Contains state information and event data associated with a routed event.</param>
        private void GridViewColumnHeaderClickedHandler(object sender, RoutedEventArgs e)
        {
            // Get the clicked column
            var headerClicked = e.OriginalSource as GridViewColumnHeader;

            if (headerClicked == null) return;
            if (headerClicked.Role == GridViewColumnHeaderRole.Padding) return;

            // Get the header
            var header = (string)headerClicked.Column.Header;
            if (header.Equals("Cover") || header.Equals("Songs")) return;

            // Get sort direction
            var direction = (!Equals(headerClicked, _lastHeaderClicked)) ? 
                ListSortDirection.Ascending : 
                ((_lastDirection == ListSortDirection.Ascending) ? 
                    ListSortDirection.Descending : 
                    ListSortDirection.Ascending);

            // Sort data
            var dataView = CollectionViewSource.GetDefaultView(releasesListView.ItemsSource);
            dataView.SortDescriptions.Clear();
            dataView.SortDescriptions.Add(new SortDescription(header, direction));
            dataView.Refresh();

            // Set header arrow
            headerClicked.Column.HeaderTemplate = 
                (direction == ListSortDirection.Ascending) ? 
                Resources["HeaderTemplateArrowUp"] as DataTemplate : 
                Resources["HeaderTemplateArrowDown"] as DataTemplate;

            // Remove arrow from previously sorted header  
            if (_lastHeaderClicked != null && _lastHeaderClicked != headerClicked) _lastHeaderClicked.Column.HeaderTemplate = null;

            _lastHeaderClicked = headerClicked;
            _lastDirection = direction;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            // Navigate to the link with the default browser
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private async void discardedButton_Click(object sender, RoutedEventArgs e)
        {
            if (discardedButton.Content.ToString().Contains("discarded"))
            {
                if (Discarded == null)
                {
                    MessageBox.Show("No discarded releases", "Discarded", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (Discarded.Count == 0)
                {
                    MessageBox.Show("No discarded releases", "Discarded", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                releasesListView.ItemsSource = null;
                releasesListView.Items.Clear();
                await Dispatcher.InvokeAsync(() =>
                {
                    releasesListView.ItemsSource = Discarded;
                    DataContext = Discarded;
                    discardedButton.Content = "Go back";
                    discardMenuItem.Header = "Put back";
                    compareItunesButton.IsEnabled = false;
                    CompareWithListMenuItem.IsEnabled = false;
                    CompareWithBoth.IsEnabled = false;
                });
            }
            else
            {
                if (iTunesCompareTabItem.IsSelected)
                {
                    songTabItem.IsSelected = true;
                    compareItunesButton.Header = "Compare with iTunes";
                    compareItunesButton.IsEnabled = true;
                    CompareWithListMenuItem.IsEnabled = true;
                    CompareWithBoth.IsEnabled = true;
                    discardedButton.Content = "Show discarded";
                    discardMenuItem.Header = "Discard";
                }
                else
                {
                    releasesListView.ItemsSource = null;
                    releasesListView.Items.Clear();
                    await Dispatcher.InvokeAsync(() =>
                    {
                        releasesListView.ItemsSource = Releases;
                        DataContext = Releases;
                        discardedButton.Content = "Show discarded";
                        discardMenuItem.Header = "Discard";
                        compareItunesButton.IsEnabled = true;
                        CompareWithListMenuItem.IsEnabled = true;
                        CompareWithBoth.IsEnabled = true;
                    });
                }
            }
        }

        private async Task<StringBuilder> GetStringBuilderToSaveLinkAsync(IEnumerable<Release> collection, int counter, int totalItems, CancellationToken token)
        {
            StringBuilder builder = new StringBuilder();
            foreach (Release item in collection)
            {
                token.ThrowIfCancellationRequested();

                counter++;
                await UpdateLayout(counter, "Saving link " + counter.ToString() + " of " + totalItems.ToString());

                builder.AppendLine(item.Link);
            }

            return builder;
        }

        private async void saveLinksMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CancellationToken token = GetCancellationToken();
            StringBuilder builder = new StringBuilder();

            await PrepareLayout(Releases.Count + Discarded.Count, "Saving link 1 of " + (Releases.Count + Discarded.Count).ToString());

            try
            {
                int counter = 0;
                builder = await GetStringBuilderToSaveLinkAsync(Releases, counter, Releases.Count + Discarded.Count, token);
                
                if (Discarded != null && Discarded.Count > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine("Discarded:");

                    builder.AppendLine((await GetStringBuilderToSaveLinkAsync(Discarded, counter, Releases.Count + Discarded.Count, token)).ToString());
                }

                builder.Length = builder.Length - 1;
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Task cancelled", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            using (StreamWriter writer = File.CreateText(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + @"\link_list.txt"))
            {
                await writer.WriteAsync(builder.ToString());
            }

            await CleanupLayout();
            MessageBox.Show("Links list saved. You can find it at " + Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + @"\link_list.txt", "Links saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void discardMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await DiscardOrRemoveAsync(true);
        }

        private async void removeLinkMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await DiscardOrRemoveAsync(false);
        }

        private async Task DiscardOrRemoveAsync(bool discard)
        {
            if (releasesListView.SelectedItem == null) return;

            CancellationToken token = GetCancellationToken();

            var selected = releasesListView.SelectedItems.Cast<Release>().ToList();

            if (discard)
            {
                await PrepareLayout(selected.Count, "Discarding link 1 of " + selected.Count.ToString());
            }
            else
            {
                await PrepareLayout(selected.Count, "Removing link 1 of " + selected.Count.ToString());
            }

            try
            {
                if (discardedButton.Content.ToString().Contains("discarded"))
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        foreach (Release item in selected)
                        {
                            token.ThrowIfCancellationRequested();

                            Releases.Remove(item);
                            if (discard) Discarded.Add(item);
                        }
                    }, System.Windows.Threading.DispatcherPriority.Normal, token);
                }
                else
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        foreach (Release item in selected)
                        {
                            token.ThrowIfCancellationRequested();

                            if (discard) Releases.Add(item);
                            Discarded.Remove(item);
                        }
                    }, System.Windows.Threading.DispatcherPriority.Normal, token);
                }
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Task cancelled", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                await CleanupLayout();
            }
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            _tokenSource.Cancel();
        }

        private async void quickSearchButton_Click(object sender, RoutedEventArgs e)
        {
            DateTime? startDate = null;
            DateTime? endDate = DateTime.Today;

            var onlineDate = CalendarWindow.OnlineSettings();
            if (onlineDate != new DateTime())
            {
                startDate = onlineDate >= Properties.Settings.Default.LastCheck ? onlineDate : Properties.Settings.Default.LastCheck;
            }
            else
            {
                startDate = Properties.Settings.Default.LastCheck;
            }

            // Check if the dates have value
            if (startDate == null || endDate == null)
            {
                MessageBox.Show("Please select a valid start and end date", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Check if the selected period is correct
            if (startDate > endDate)
            {
                MessageBox.Show("Start date cannot be later than the end date", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Get the cancellation token
            var token = GetCancellationToken();

            // Set UI description TextBlock
            selectedLinkFileTextBlock.Text = (startDate.Value == endDate.Value) ?
                "Showing releases of " + startDate.Value.ToString("dd/MM/yyyy") :
                "Showing releases from " + startDate.Value.ToString("dd/MM/yyyy") + " to " + endDate.Value.ToString("dd/MM/yyyy");

            // Initialize a private collection that will hold the releases
            var releases = new ObservableCollection<Release>();

            try
            {
                releases = await QuickSearchByDate(startDate, endDate, releases, token);
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Task cancelled", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (WebException)
            {
                MessageBox.Show("Task cancelled", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // Set the data context and show the releases
            DataContext = Releases;
            await Dispatcher.InvokeAsync(() => { releasesListView.ItemsSource = Releases; });

            // Final UI touches
            await CleanupLayout();

            // Download the album artworks previously added to the list
            await Dispatcher.InvokeAsync(DownloadCover);
        }

        private async void dateChooseButton_Click(object sender, RoutedEventArgs e)
        {
            // Initialize and show the window with the date calendars
            var calendar = new CalendarWindow();
            calendar.ShowDialog();

            // Get the start and end date
            var startDate = (calendar.DialogResult == true) ? calendar.StartDatePicker.SelectedDate : null;
            var endDate = (calendar.DialogResult == true) ? calendar.EndDatePicker.SelectedDate : null;

            // Check if the dates have value
            if (startDate == null || endDate == null)
            {
                MessageBox.Show("Please select a valid start and end date", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Check if the selected period is correct
            if (startDate > endDate)
            {
                MessageBox.Show("Start date cannot be later than the end date", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Get the cancellation token
            var token = GetCancellationToken();

            // Set UI description TextBlock
            selectedLinkFileTextBlock.Text = (startDate.Value == endDate.Value) ?
                "Showing releases of " + startDate.Value.ToString("dd/MM/yyyy") :
                "Showing releases from " + startDate.Value.ToString("dd/MM/yyyy") + " to " + endDate.Value.ToString("dd/MM/yyyy");

            // Initialize a private collection that will hold the releases
            var releases = new ObservableCollection<Release>();

            try
            {
                releases = await QuickSearchByDate(startDate, endDate, releases, token);
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Task cancelled", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (WebException)
            {
                MessageBox.Show("Task cancelled", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // Set the data context and show the releases
            DataContext = Releases;
            await Dispatcher.InvokeAsync(() => { releasesListView.ItemsSource = Releases; });

            // Final UI touches
            await CleanupLayout();

            // Download the album artworks previously added to the list
            await Dispatcher.InvokeAsync(DownloadCover);
        }

        private async Task<ObservableCollection<Release>> QuickSearchByDate(DateTime? startDate, DateTime? endDate, ObservableCollection<Release> releases, CancellationToken token)
        {
            await Task.Run(async () =>
            {
                // Get the UI ready
                await Dispatcher.InvokeAsync(() =>
                {
                    statusProgressBar.Value = 0;
                    statusProgressBar.Visibility = Visibility.Visible;
                    cancelButton.Visibility = Visibility.Visible;

                    mainStatusBarItem.Content = "Finding pages...";
                    statusProgressBar.IsIndeterminate = true;
                });

                // Initialize some variables
                var counter = 0;
                var totalPages = 0;
                var links = new List<string>();

                // Iterate through the dates to find releases
                for (DateTime currentDate = startDate.Value; currentDate <= endDate.Value; currentDate = currentDate.AddDays(1))
                {
                    // Cancel the async task if requested
                    token.ThrowIfCancellationRequested();

                    // Get the webpage link
                    var webpageLink = @"http://techdeephouse.com/calendar/" + currentDate.ToString("yyyy/MM/dd");

                    // Download the webpage and load it to the HtmlDocument
                    var document = new HtmlDocument();
                    using (var client = new WebClient() { Encoding = Encoding.UTF8 })
                    {
                        token.Register(client.CancelAsync);
                        document.LoadHtml(await client.DownloadStringTaskAsync(webpageLink));
                    }

                    // Get the HtmlNode where the page number is located
                    var pagesNode = document.DocumentNode.SelectSingleNode("//div[@class='navigation']");

                    // If there are no pages listed and no elements, skip to the next date
                    if (pagesNode == null && document.DocumentNode.SelectSingleNode("//div[@class='base shortstory']") == null) continue;

                    // Get the number of pages
                    var pages = (pagesNode != null) ? Convert.ToInt32(pagesNode.Descendants("a").LastOrDefault()?.InnerText) : 1;

                    // Increment total pages
                    totalPages += pages;

                    // Add new link to the list for each page
                    for (int i = 1; i <= pages; i++)
                    {
                        links.Add(webpageLink + @"/page/" + i.ToString() + @"/");
                    }
                }

                // Set some UI properties
                await Dispatcher.InvokeAsync(() => statusProgressBar.IsIndeterminate = false);
                await PrepareLayout((double)totalPages, "Downloading releases from page 1 of " + totalPages.ToString());

                // Iterate through the links' list
                foreach (var url in links)
                {
                    // Cancel the async task if requested
                    token.ThrowIfCancellationRequested();

                    // Increment counter and update UI
                    counter++;
                    await UpdateLayout(counter, "Downloading releases from page " + counter.ToString() + " of " + totalPages.ToString());

                    // Download the webpage and load it to the HtmlDocument
                    var document = new HtmlDocument();
                    using (var client = new WebClient())
                    {
                        token.Register(client.CancelAsync);
                        document.Load(await client.OpenReadTaskAsync(url), Encoding.UTF8);
                    }

                    // Get a list of the HtmlNodes that holds the releases
                    var nodes = document.DocumentNode.SelectSingleNode("//div[@id='dle-content']").SelectNodes("//div[@class='base shortstory']").ToList();

                    // Iterate through the html nodes
                    foreach (HtmlNode item in nodes)
                    {
                        // Cancel the async task if requested
                        token.ThrowIfCancellationRequested();

                        // Get the release link
                        var link = item.Descendants("a").FirstOrDefault(x => x.HasAttributes && x.Attributes["class"].Value == "titlev")?.Attributes["href"].Value;

                        // Initialize properties
                        string date = string.Empty, title = string.Empty, genre = string.Empty, label = string.Empty, artist = string.Empty, coverLink = string.Empty;
                        var hasErrors = false;
                        var songs = new List<string>();
                        var subParentNodeList = new List<HtmlNode>();

                        try
                        {
                            // Get the release date
                            date = System.Web.HttpUtility.HtmlDecode(item.Descendants("div").FirstOrDefault(x => x.Attributes["class"].Value == "short-info")?.Descendants("span").FirstOrDefault(x => x.Attributes["class"].Value == "icon icon-calendar")?.InnerText);

                            // Get the album artwork link if available
                            if (item.Descendants("a").FirstOrDefault(x => x.HasAttributes && x.Attributes["class"].Value == "image-box")?.Descendants("img").FirstOrDefault() != null)
                            {
                                coverLink = "http://techdeephouse.com" + item.Descendants("a").FirstOrDefault(x => x.HasAttributes && x.Attributes["class"].Value == "image-box")?.Descendants("img").FirstOrDefault()?.Attributes["src"].Value;
                            }

                            // Get a list of the child nodes that hold the release properties
                            var tempnode = item.ChildNodes.LastOrDefault(x => x.Name == "div" && !x.HasAttributes);
                            subParentNodeList = tempnode.Descendants("div").ToList();

                            // Title
                            title = System.Web.HttpUtility.HtmlDecode(subParentNodeList.FirstOrDefault(x => x.InnerText.StartsWith("Title:") || x.InnerText.StartsWith("TItle:"))?.InnerText.Remove(0, 6).Trim());

                            // Genre
                            genre = System.Web.HttpUtility.HtmlDecode(subParentNodeList.FirstOrDefault(x => x.InnerText.StartsWith("Genre:"))?.InnerText.Remove(0, 6).Trim());

                            // Label
                            label = System.Web.HttpUtility.HtmlDecode(subParentNodeList.FirstOrDefault(x => x.InnerText.StartsWith("Label:"))?.InnerText.Remove(0, 6).Trim());

                            // Artist
                            artist = subParentNodeList.FirstOrDefault(x => x.InnerText.Contains("Artist:") && !(x.InnerText.Contains("Title:") || x.InnerText.Contains("TItle:")))?.InnerText;
                            artist = System.Web.HttpUtility.HtmlDecode(artist?.Substring(artist.IndexOf("Artist:") + 8).Trim());
                        }
                        catch (Exception)
                        {
                            // Notify if any error was encountered while parsing the release properties
                            hasErrors = true;
                        }

                        try
                        {
                            // We need to narrow the child nodes in order to get the songs of the release
                            subParentNodeList.RemoveRange(0, subParentNodeList.FindIndex(x => x.InnerText.Contains("Quality:")) + 1);

                            // Iterate through the narrowes child nodes list
                            foreach (HtmlNode node in subParentNodeList)
                            {
                                // Cancel the async task if requested
                                token.ThrowIfCancellationRequested();

                                // Add the song to the list
                                if (!string.IsNullOrWhiteSpace(node.FirstChild.InnerText) &&
                                    !node.FirstChild.InnerText.Contains("Artist:") &&
                                    !node.FirstChild.InnerText.Contains("Title:") &&
                                    !node.FirstChild.InnerText.Contains("Genre:") &&
                                    !node.FirstChild.InnerText.Contains("Label:") &&
                                    !node.FirstChild.InnerText.Contains("Quality:")) {

                                    songs.Add(System.Web.HttpUtility.HtmlDecode(node.FirstChild.InnerText.Trim()));
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // If any error was encountered while getting the songs,
                            // notify it and return an empty list
                            songs = new List<string>();
                            hasErrors = true;
                        }

                        // Initialize a defualt album artwork,
                        // we'll download them later.
                        BitmapImage cover = new BitmapImage();
                        cover.Freeze();

                        // If the cover link is valid, add it to the list
                        // so we'll able to download it later
                        if (!coverLink.StartsWith("http")) coverLink = "";
                        if (!Uri.TryCreate(coverLink, UriKind.Absolute, out Uri uriResult)) coverLink = "";
                        if (uriResult != null)
                        {
                            if (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps) coverLink = "";
                        }
                        if (!string.IsNullOrWhiteSpace(coverLink)) _coverUrls.Add(link, coverLink);

                        // Add the release to the private list
                        releases.Add(new Release(artist, genre, date, title, label, link, songs, cover, hasErrors));
                    }
                }
            }, token);

            // Set the last check
            Properties.Settings.Default.LastCheck = DateTime.Parse(DateTime.Today.ToString("dd/MM/yyyy"));
            Properties.Settings.Default.Save();

            // Save settings into the OneDrive folder
            var oneDriveFolderPath = Environment.GetEnvironmentVariable("OneDriveConsumer", EnvironmentVariableTarget.User);
            if (Directory.Exists(oneDriveFolderPath))
            {
                if (Directory.Exists(oneDriveFolderPath + "\\Documents\\MusicReleaseAnalyzer"))
                {
                    var settingsPath = oneDriveFolderPath + "\\Documents\\MusicReleaseAnalyzer";
                    if (File.Exists(settingsPath + "\\settings.dat"))
                    {
                        try
                        {
                            File.WriteAllText(settingsPath + "\\settings.dat", DateTime.Today.ToString("dd/MM/yyyy"));
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Error while saving settings online:\r\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
            }

            // Set UI for the filter
            await PrepareLayout(releases.Count, "Filtering release 1 of " + releases.Count.ToString());

            // Get the filtered release list
            return Releases = await FilterReleasesAsync(releases, token);
        }

        private void copyLinkMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (releasesListView.SelectedItems.Count == 0) return;

            StringBuilder builder = new StringBuilder();

            var selected = releasesListView.SelectedItems.Cast<Release>().ToList();
            foreach (Release item in selected)
            {
                builder.AppendLine(item.Link);
            }

            builder.Length = builder.Length - 1;

            Clipboard.SetText(builder.ToString());
        }

        private async void compareItunesButton_Click(object sender, RoutedEventArgs e)
        {
            if (Releases.Count == 0)
            {
                MessageBox.Show("Populate releases list first", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            OpenFileDialog dialog = new OpenFileDialog();
            bool? result = dialog.ShowDialog();

            if (!result.HasValue || !result.Value) return;
            iTunesCompareTabItem.IsSelected = true;
            discardedButton.Content = "Go back";

            CancellationToken token = GetCancellationToken();

            List<string> finalSongs = new List<string>();
            try
            {
                List<string> releasedSongs = new HashSet<string>(Releases.SelectMany(x => x.Songs)).ToList();

                await PrepareLayout(releasedSongs.Count, "Getting iTunes Library");

                var library = new ITunesLibrary(dialog.FileName);
                List<Track> itunesTracks = library.Tracks.ToList();

                int counter = 0;
                await Task.Run(async () =>
                {
                    foreach (string song in releasedSongs)
                    {
                        token.ThrowIfCancellationRequested();

                        counter++;
                        await UpdateLayout(counter, "Comparing song " + counter.ToString() + " of " + releasedSongs.Count.ToString() + " to iTunes Library's songs");

                        Match match = null;
                        foreach (string pattern in _regexPatterns)
                        {
                            token.ThrowIfCancellationRequested();

                            Regex regex = new Regex(pattern);
                            if (!regex.IsMatch(song)) continue;
                            match = regex.Match(song);
                            break;
                        }

                        string releasedSongTitle = match?.Groups["title"].Value.TrimEnd() ?? "";
                        string releasedSongArtist = match?.Groups["artist"].Value ?? "";

                        if (string.IsNullOrWhiteSpace(releasedSongArtist) || string.IsNullOrWhiteSpace(releasedSongTitle)) continue;

                        List<string> releaseArtist = new List<string>();
                        if (_separator.Any(releasedSongArtist.Contains))
                        {
                            releaseArtist.AddRange(releasedSongArtist.Split(_separator, StringSplitOptions.RemoveEmptyEntries).ToList());
                        }
                        else
                        {
                            releaseArtist.Add(releasedSongArtist);
                        }

                        bool exists = false;
                        foreach (Track track in itunesTracks)
                        {
                            token.ThrowIfCancellationRequested();

                            List<string> trackArtist = new List<string>();
                            if (_separator.Any(track.Artist.Contains))
                            {
                                trackArtist.AddRange(track.Artist.Split(_separator, StringSplitOptions.RemoveEmptyEntries).ToList());
                            }
                            else
                            {
                                trackArtist.Add(track.Artist);
                            }

                            if (releasedSongTitle == track.Name)
                            {
                                if (trackArtist.Count == releaseArtist.Count)
                                {
                                    if (trackArtist.All(releaseArtist.Contains))
                                    {
                                        exists = true;
                                    }
                                }
                            }
                        }

                        string editedSong = releasedSongArtist + " - " + releasedSongTitle;
                        if (!exists && !finalSongs.Contains(editedSong))
                        {
                            finalSongs.Add(editedSong);
                        }
                    }
                }, token);
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Task cancelled", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            await Dispatcher.InvokeAsync(() =>
            {
                iTunesComparedListView.ItemsSource = null;
                iTunesComparedListView.Items.Clear();
                iTunesComparedListView.ItemsSource = finalSongs;
            });
            await CleanupLayout();
            compareItunesButton.IsEnabled = false;
        }

        private async void saveSongMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CancellationToken token = GetCancellationToken();
            StringBuilder builder = new StringBuilder();

            List<string> songs = iTunesComparedListView.Items.Cast<string>().ToList();
            await PrepareLayout(songs.Count, "Saving song 1 of " + songs.Count.ToString());

            try
            {
                int counter = 0;
                await Task.Run(async () =>
                {
                    foreach (string item in songs)
                    {
                        token.ThrowIfCancellationRequested();

                        counter++;
                        await UpdateLayout(counter, "Saving song " + counter.ToString() + " of " + songs.Count.ToString());

                        builder.AppendLine(item);
                    }
                }, token);

                builder.Length = builder.Length - 1;
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Task cancelled", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            using (StreamWriter writer = File.CreateText(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + @"\song_list.txt"))
            {
                await writer.WriteAsync(builder.ToString());
            }

            await CleanupLayout();
            MessageBox.Show("Songs list saved. You can find it at " + Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + @"\song_list.txt", "Songs saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task<List<Tuple<string, string, List<string>>>> GetDictionaryAsync(string path, CancellationToken token)
        {
            List<string> songs = new HashSet<string>(await ReadDataFromFileAsync(path, token)).ToList();

            var list = new List<Tuple<string, string, List<string>>>();
            await Task.Run(() =>
            {
                foreach (var song in songs)
                {
                    token.ThrowIfCancellationRequested();

                    Match match = null;
                    foreach (string pattern in _regexPatterns)
                    {
                        token.ThrowIfCancellationRequested();

                        Regex regex = new Regex(pattern);
                        if (!regex.IsMatch(song)) continue;
                        match = regex.Match(song);
                        break;
                    }

                    string releasedSongTitle = match?.Groups["title"].Value.TrimEnd() ?? "";
                    string releasedSongArtist = match?.Groups["artist"].Value ?? "";

                    if (string.IsNullOrWhiteSpace(releasedSongArtist) || string.IsNullOrWhiteSpace(releasedSongTitle)) continue;

                    List<string> releaseArtist = new List<string>();
                    if (_separator.Any(releasedSongArtist.Contains))
                    {
                        releaseArtist.AddRange(releasedSongArtist.Split(_separator, StringSplitOptions.RemoveEmptyEntries).ToList());
                    }
                    else
                    {
                        releaseArtist.Add(releasedSongArtist);
                    }

                    list.Add(Tuple.Create(releasedSongTitle, releasedSongArtist, releaseArtist));
                }
            }, token);

            return list;
        }

        private async void CompareWithListMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (Releases.Count == 0)
            {
                MessageBox.Show("Populate releases list first", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            OpenFileDialog dialog = new OpenFileDialog();
            bool? result = dialog.ShowDialog();

            if (!result.HasValue || !result.Value) return;
            iTunesCompareTabItem.IsSelected = true;
            discardedButton.Content = "Go back";

            CancellationToken token = GetCancellationToken();

            List<string> finalSongs = new List<string>();
            try
            {
                await PrepareLayout(1, "Getting songs lists ready");

                List<string> releasedSongs = new HashSet<string>(Releases.SelectMany(x => x.Songs)).ToList();
                var extSongsList = await GetDictionaryAsync(dialog.FileName, token);

                await PrepareLayout(releasedSongs.Count, "Getting songs lists ready");

                int counter = 0;
                await Task.Run(async () =>
                {
                    foreach (string song in releasedSongs)
                    {
                        token.ThrowIfCancellationRequested();

                        counter++;
                        await UpdateLayout(counter, "Comparing song " + counter.ToString() + " of " + releasedSongs.Count.ToString() + " to external list's songs");

                        Match match = null;
                        foreach (string pattern in _regexPatterns)
                        {
                            token.ThrowIfCancellationRequested();

                            Regex regex = new Regex(pattern);
                            if (!regex.IsMatch(song)) continue;
                            match = regex.Match(song);
                            break;
                        }

                        string releasedSongTitle = match?.Groups["title"].Value.TrimEnd() ?? "";
                        string releasedSongArtist = match?.Groups["artist"].Value ?? "";

                        if (string.IsNullOrWhiteSpace(releasedSongArtist) || string.IsNullOrWhiteSpace(releasedSongTitle)) continue;

                        List<string> releaseArtist = new List<string>();
                        if (_separator.Any(releasedSongArtist.Contains))
                        {
                            releaseArtist.AddRange(releasedSongArtist.Split(_separator, StringSplitOptions.RemoveEmptyEntries).ToList());
                        }
                        else
                        {
                            releaseArtist.Add(releasedSongArtist);
                        }

                        bool exists = false;
                        foreach (var track in extSongsList)
                        {
                            token.ThrowIfCancellationRequested();

                            List<string> trackArtist = new List<string>();
                            if (_separator.Any(track.Item2.Contains))
                            {
                                trackArtist.AddRange(track.Item2.Split(_separator, StringSplitOptions.RemoveEmptyEntries).ToList());
                            }
                            else
                            {
                                trackArtist.Add(track.Item2);
                            }

                            if (releasedSongTitle == track.Item1)
                            {
                                if (trackArtist.Count == releaseArtist.Count)
                                {
                                    if (trackArtist.All(releaseArtist.Contains))
                                    {
                                        exists = true;
                                    }
                                }
                            }
                        }

                        string editedSong = releasedSongArtist + " - " + releasedSongTitle;
                        if (!exists && !finalSongs.Contains(editedSong))
                        {
                            finalSongs.Add(editedSong);
                        }
                    }
                }, token);
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Task cancelled", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            await Dispatcher.InvokeAsync(() =>
            {
                iTunesComparedListView.ItemsSource = null;
                iTunesComparedListView.Items.Clear();
                iTunesComparedListView.ItemsSource = finalSongs;
            });
            await CleanupLayout();
        }

        private bool SongExits(string artist, string title1, string title2, ICollection<string> releaseArtistsList)
        {
            List<string> trackArtist = new List<string>();
            if (_separator.Any(artist.Contains))
            {
                trackArtist.AddRange(artist.Split(_separator, StringSplitOptions.RemoveEmptyEntries).ToList());
            }
            else
            {
                trackArtist.Add(artist);
            }

            if (title1 == title2)
            {
                if (trackArtist.Count == releaseArtistsList.Count)
                {
                    if (trackArtist.All(releaseArtistsList.Contains))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private async void CompareWithBoth_Click(object sender, RoutedEventArgs e)
        {
            if (Releases.Count == 0)
            {
                MessageBox.Show("Populate releases list first", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "Select iTunes Library XML file";
            bool? result = dialog.ShowDialog();

            if (!result.HasValue || !result.Value) return;

            string iTunesXmlPath = dialog.FileName;

            dialog.Title = "Select external list txt file";
            result = dialog.ShowDialog();
            if (!result.HasValue || !result.Value) return;

            string extListPath = dialog.FileName;

            iTunesCompareTabItem.IsSelected = true;
            discardedButton.Content = "Go back";

            CancellationToken token = GetCancellationToken();

            List<string> finalSongs = new List<string>();
            try
            {
                await PrepareLayout(1, "Getting songs lists ready");

                List<string> releasedSongs = new HashSet<string>(Releases.SelectMany(x => x.Songs)).ToList();
                var extSongsList = await GetDictionaryAsync(extListPath, token);

                var library = new ITunesLibrary(dialog.FileName);
                List<Track> itunesTracks = library.Tracks.ToList();

                await PrepareLayout(releasedSongs.Count, "Getting songs lists ready");

                int counter = 0;
                await Task.Run(async() =>
                {
                    foreach (var song in releasedSongs)
                    {
                        token.ThrowIfCancellationRequested();

                        counter++;
                        await UpdateLayout(counter, "Comparing song " + counter.ToString() + " of " + releasedSongs.Count.ToString());

                        Match match = null;
                        foreach (string pattern in _regexPatterns)
                        {
                            token.ThrowIfCancellationRequested();

                            Regex regex = new Regex(pattern);
                            if (!regex.IsMatch(song)) continue;
                            match = regex.Match(song);
                            break;
                        }

                        string releasedSongTitle = match?.Groups["title"].Value.TrimEnd() ?? "";
                        string releasedSongArtist = match?.Groups["artist"].Value ?? "";

                        if (string.IsNullOrWhiteSpace(releasedSongArtist) || string.IsNullOrWhiteSpace(releasedSongTitle)) continue;

                        List<string> releaseArtist = new List<string>();
                        if (_separator.Any(releasedSongArtist.Contains))
                        {
                            releaseArtist.AddRange(releasedSongArtist.Split(_separator, StringSplitOptions.RemoveEmptyEntries).ToList());
                        }
                        else
                        {
                            releaseArtist.Add(releasedSongArtist);
                        }

                        bool exists = false;
                        foreach (var track in extSongsList)
                        {
                            token.ThrowIfCancellationRequested();

                            exists = SongExits(track.Item2, releasedSongArtist, track.Item1, releaseArtist);
                        }

                        foreach (Track track in itunesTracks)
                        {
                            token.ThrowIfCancellationRequested();

                            exists = SongExits(track.Artist, releasedSongTitle, track.Name, releaseArtist);
                        }

                        string editedSong = releasedSongArtist + " - " + releasedSongTitle;
                        if (!exists && !finalSongs.Contains(editedSong))
                        {
                            finalSongs.Add(editedSong);
                        }
                    }
                }, token);
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Task cancelled", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            await Dispatcher.InvokeAsync(() =>
            {
                iTunesComparedListView.ItemsSource = null;
                iTunesComparedListView.Items.Clear();
                iTunesComparedListView.ItemsSource = finalSongs;
            });
            await CleanupLayout();
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Version " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString() + "\r\n\r\nIcon made by Smashicons from www.flaticon.com", "About Taggify", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
