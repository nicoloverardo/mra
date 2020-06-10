using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media.Imaging;

namespace MusicReleaseAnalyzer
{
    /// <summary>
    /// Release model
    /// </summary>
    public class Release : INotifyPropertyChanged
    {
        private string _artist, _genre, _date, _title, _label, _link;
        private bool _hasErrors;
        private List<string> _songs;
        private BitmapImage _cover;

        /// <summary>
        /// Represents a new release item.
        /// </summary>
        /// <param name="artist">The artist</param>
        /// <param name="genre">The genre</param>
        /// <param name="date">The date of the release</param>
        /// <param name="title">The title</param>
        /// <param name="label">The label that released this album/ep/lp/single</param>
        /// <param name="link">The original link to the release</param>
        /// <param name="songs">The song(s) contained</param>
        /// <param name="cover">The cover</param>
        /// <param name="hasErrors">Bool that indicates if any error was encountered while parsing the release</param>
        public Release(string artist, string genre, string date, string title, string label, string link, List<string> songs, BitmapImage cover, bool hasErrors)
        {
            _artist = artist;
            _genre = genre;
            _date = date;
            _title = title;
            _label = label;
            _songs = songs;
            _cover = cover;
            _link = link;
            _hasErrors = hasErrors;
        }

        public string Artist
        {
            get => _artist;
            set
            {
                _artist = value;
                OnPropertyChanged("Artist");
            }
        }

        public string Genre
        {
            get => _genre;
            set
            {
                _genre = value;
                OnPropertyChanged("Genre");
            }
        }

        public string Date
        {
            get => _date;
            set
            {
                _date = value;
                OnPropertyChanged("Date");
            }
        }

        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                OnPropertyChanged("Title");
            }
        }

        public string Label
        {
            get => _label;
            set
            {
                _label = value;
                OnPropertyChanged("Label");
            }
        }

        public string Link
        {
            get => _link;
            set
            {
                _link = value;
                OnPropertyChanged("Link");
            }
        }

        public List<string> Songs
        {
            get => _songs;
            set
            {
                _songs = value;
                OnPropertyChanged("Songs");
            }
        }

        public BitmapImage Cover
        {
            get => _cover;
            set
            {
                _cover = value;
                OnPropertyChanged("Cover");
            }
        }

        public bool HasErrors
        {
            get => _hasErrors;
            set
            {
                _hasErrors = value;
                OnPropertyChanged("HasErrors");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
