# Music Release Analyzer
Written in C#, it is a simple WPF app that scrapes data from http://techdeephouse.com/ in order to help you keeping up to date with new music releases. It remembers the last time you used it so it will download only music released from that day forward. Most importantly, it filters out releases of those artists and labels not present in the `artists.txt` and `labels.txt`. You can also copy or save to a txt file the links of the releases and load them back later.

---

iTunes parser dll is based on the work of asciamanna. The original repository can be found [here](https://github.com/asciamanna/iTunesLibraryParser). The icon is made by Smashicons from www.flaticon.com.
