using Microsoft.Win32;
using EpubReaderWithAnnotations.Utilities;
using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Xml.Linq;
using mshtml;
//using System.Windows.Shapes;

namespace EpubReaderWithAnnotations
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string neutralColor = "#FFFF00";
        private static XElement annotCategories;
        private static string[][] mainCategories;
        private static string[][] subCategories;
        private static AnnotationEdit editWindow = new AnnotationEdit();
        private static SearchWindow searchWindow;
        //private string bookAuthor, bookTitle, bookLanguage;
        string[] bookData;
        private static BookData book;
        private BrowserOperations browser;
        private string _tempPath; // library dir
        private string _baseMenuXmlDiretory;
        private List<string> _menuItems; //book chapters?
        private string[] bookFileNames; // same as the list but as array, for testing only
        private int _currentPage;
        private XDocument currentAnnotes,bookAnnotes;
        private XDocument nounsFile, verbsFile,excFile;
        private List<string> exclusions;
        private bool isBookAnnotes; //otherwise isGrammarAnnotes
        private bool bookAnnotesChanged;
        private List<XElement>[] annotesByChapter;
        private IEnumerable<XElement> chapterAnnotes;
        private List<IHTMLElement> initialColl; //collection of tags for one chapter
        private List<string>[] fullBookText;
        private string currentLanguage = null;
        private string mainQuery;
        private string doubtQuery;
        private bool inBrowseMode = false;
        private int browsingStart = 0;
        private int browsingFinish = 0;
        private int currentlyBrowsing = 0;
        private int _listBoxPrevInd = -1;
        private bool pageLoaded = false;
        private bool searchOn = false;
        private bool annotationsShown = true;
        private string searchItem;
        private XDocument searchResult;
        private HTMLDocumentEvents2_Event _docEvent;
        private IHTMLDocument2 docOrg;
        bool isAssigned = false;
        BackgroundWorker searcher;


        
        public MainWindow()
        {
            InitializeComponent();
            setSearcher();
            
            epubDisplay.LoadCompleted += delegate
            {
                if (_docEvent != null /* && !isAssigned -- not needed?? */)
                {
                    _docEvent.ondblclick -= _docEvent_ondblclick;
                    _docEvent.oncontextmenu -= _docEvent_oncontextmenu;

                }
                if (epubDisplay.Document != null)
                {
                    _docEvent = (HTMLDocumentEvents2_Event)epubDisplay.Document;
                    _docEvent.ondblclick += _docEvent_ondblclick;
                    _docEvent.oncontextmenu += _docEvent_oncontextmenu;
                    
                }
            }; 

            editWindow.AnnotationChangesConfirmed += (s, e) =>
            {
                annotesByChapter[_currentPage] = editWindow.NewAnnotes;
                epubDisplay.Navigate(GetPath(_currentPage));
            };

            
            _menuItems = new List<string>();//book chapters - filenames
            NextButton.Visibility = Visibility.Hidden;
            PreviousButton.Visibility = Visibility.Hidden;
            annotCategories = XElement.Load("categories.xml");
            mainCategories = readCategoryList();
            subCategories = createSubCategriesArray();
            //string[] subCats = getSubCategories("Dictionary");
            //int i = 444;
            
        }

        bool _docEvent_oncontextmenu (IHTMLEventObj pEvtObj)
        {
            WbShowContextMenu();
            return false;
        }

        private void toggleMenuItem (object menuItem, bool value, int subNr=0)
        {
            MenuItem item = (MenuItem)menuItem;
            item.IsEnabled = value;
        }
        public void WbShowContextMenu()
        {
            ContextMenu cm = FindResource("MenuCustom") as ContextMenu;
            if (cm == null) return;
            MenuItem first = (MenuItem)cm.Items.GetItemAt(0);
            toggleMenuItem(first.Items.GetItemAt(0), isBookAnnotes);
            toggleMenuItem(first.Items.GetItemAt(1), isBookAnnotes);
            toggleMenuItem(first.Items.GetItemAt(2), isBookAnnotes);
            toggleMenuItem(first.Items.GetItemAt(3), isBookAnnotes);
            toggleMenuItem(first.Items.GetItemAt(4), !isBookAnnotes);
            toggleMenuItem(cm.Items.GetItemAt(3), !isBookAnnotes);

            cm.PlacementTarget = epubDisplay;
            cm.IsOpen = true;
        }

        private void setSearcher ()
        {
            searcher = new BackgroundWorker();
            searcher.WorkerReportsProgress = true;
            searcher.DoWork += searcherDoWork;
            searcher.ProgressChanged += searcherProgressChanged;
            searcher.RunWorkerCompleted += searcherRunWorkerCompleted;
        }

        void searcherDoWork(object sender, DoWorkEventArgs e)
        {
            TextSearch.searchText(fullBookText, bookData, _baseMenuXmlDiretory, bookFileNames,mainQuery,doubtQuery,exclusions);
        }

        void searcherProgressChanged(object sender, ProgressChangedEventArgs e)
        {

        }

        void searcherRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            currentAnnotes = TextSearch.SearchResult;
            annotesByChapter = divideAnnotesByChapter();
            NavigateToPage(_currentPage);
        }

        private static string[][] readCategoryList ()
        {
            var categories = from cat in annotCategories.Elements("category")
                             select cat;
            int len = categories.Count();
            XElement single;
            string[][] result = new string[2][];
            result[0] = new string[len];
            result[1] = new string[len];
            for (int i = 0; i<len;i++)
            {
                single = categories.ElementAt(i);
                result[0][i] = single.Attribute("name").Value;
                result[1][i] = single.Attribute("colour").Value;
            }
            return result;
        }

        public static string[][] getCategoryArray()
        {
            return mainCategories;
        }

        public static string[][] getSubCategoryArray()
        {
            return subCategories;
        }
        private static string[] getSubCategories(string category)
        {
            var subCategories = from cat in annotCategories.Descendants("subcategory1")
                                where cat.Parent.Attribute("name").Value == category
                                select cat;
            int len = subCategories.Count();
            XElement single;
            string[] result = new string[len];
            for (int i=0; i<len; i++)
            {
                single = subCategories.ElementAt(i);
                result[i] = single.Attribute("name").Value;
            }
            return result;
            
        }
        private static string[][] createSubCategriesArray()
        {
            string[][] result = new string[mainCategories[0].Length][];
            string[] currentSubCategories;
            for (int i = 0; i < mainCategories[0].Length; i++)
            {
                currentSubCategories = getSubCategories(mainCategories[0][i]);
                result[i] = currentSubCategories;
            }
            return result;

        }

        private void certainTextSearch(string s)
        {
            IHTMLTxtRange range = getRange();
            IHTMLTxtRange first = null;
            bool running = true;
            while (running)
            {
                if (range.findText(s, s.Length, 0))
                {
                    if (range != first)
                    {
                        
                        range.execCommand("BackColor", false, "#42f5b9");
                    }
                    else if (first != null)
                    {
                        running = false;
                        
                    }
                    if (first == null)
                    {
                        first = range.duplicate();
                    }
                    else
                    {
                        range.moveStart("character");
                    }



                }
                else running = false;

            }
        }

        private  void saveAnnotesToFile(string path)
        {
            XElement root = new XElement("annotations");
            XDocument file = new XDocument(root);
            List<XElement> currentList;
            for (int i = 0; i<annotesByChapter.Length; i++)
            {
                currentList = annotesByChapter[i];
                foreach (XElement element in currentList)
                    root.Add(element);
            }
            file.Save(path);
        }

        bool _docEvent_ondblclick(IHTMLEventObj pEvtObj)
        {
            if (epubDisplay.Document!=null)
            {
                IHTMLDocument2 document = epubDisplay.Document as IHTMLDocument2;
                if (document!=null)
                {
                                        
                    IHTMLSelectionObject currentSelection = document.selection;
                    IHTMLTxtRange range, rightRange;
                    range = currentSelection.createRange();
                    IHTMLElement el = range.parentElement().parentElement; // what if text is not highlighted?
                    int i = initialColl.FindIndex(elm => elm == el);
                    rightRange = compareRanges(i, range);
                    rightRange.select();
                    //range.execCommand("BackColor", false, "384377"); 
                }
            }
            return false;
        }

        private void loadLanguageFiles (string language)
        {
            excFile = XDocument.Load(currentLanguage + ".ex");
            nounsFile = XDocument.Load(currentLanguage + ".noun");
            verbsFile = XDocument.Load(currentLanguage + ".verb");
        }

        private void addWordsToLanguageFile(XDocument doc, string word)
        {
            if (word == null)
                return;
            word = word.ToLower();
            List<string> l = doc.Descendants("word")
                .Where(c => c.Value == word)
                .Select(item => item.Value)
                .ToList();
            if (l.Count == 0)
            {
                XElement el = new XElement("word");
                el.Value = word;
                doc.Root.Add(el);
            }
        }

        private XDocument createEmptyAnnotDocument ()
        {
            XAttribute bAuthor = new XAttribute("author", bookData[0]);
            XAttribute bTitle = new XAttribute("title", bookData[1]);
            XAttribute bLanguage = new XAttribute("language", bookData[2]);
            XAttribute anType = new XAttribute("type", "book");
            XElement root = new XElement("annotations", bAuthor, bTitle, bLanguage,anType);
            return new XDocument(root);
        }


        private void btnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Epub files (*.epub)|*.epub|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                string fileName = Path.GetFileNameWithoutExtension(filePath);

                if (!Directory.Exists("Library"))
                {
                    Directory.CreateDirectory("Library");
                }
                File.Copy(openFileDialog.FileName, Path.Combine("Library", fileName + ".zip"), true);
                _tempPath = Path.Combine("Library", fileName);
                if (Directory.Exists(_tempPath))
                {
                    FileUtility.DeleteDirectory(_tempPath);
                }
                FileUtility.UnZIPFiles(Path.Combine("Library", fileName + ".zip"), Path.Combine("Library", fileName));

                var containerReader = XDocument.Load(ConvertToMemmoryStream(Path.Combine("Library", fileName, "META-INF", "container.xml")));

                var baseMenuXmlPath = containerReader.Root.Descendants(containerReader.Root.GetDefaultNamespace() + "rootfile").First().Attribute("full-path").Value;
                XDocument menuReader = XDocument.Load(Path.Combine(_tempPath, baseMenuXmlPath));
                _baseMenuXmlDiretory = Path.GetDirectoryName(baseMenuXmlPath);
                XNamespace ns = menuReader.Root.Name.Namespace;
                XNamespace dc = menuReader.Root.GetNamespaceOfPrefix("dc");
                XElement metadata = menuReader.Root.Element(ns+"metadata");
                string author=metadata.Element(dc+"creator").Value;
                string title=metadata.Element(dc+"title").Value;
                string language = metadata.Element(dc + "language").Value;
                if (language!=currentLanguage)
                {
                    currentLanguage = language;
                    loadLanguageFiles(language);
                    
                }
                searchWindow = getSearchWindow(language);
                bookData = new string[] { author, title, language };

                var menuItemsIds = menuReader.Root.Element(ns + "spine").Descendants().Select(x => x.Attribute("idref").Value).ToList();
                _menuItems = menuReader.Root.Element(ns + "manifest").Descendants().Where(mn => menuItemsIds.Contains(mn.Attribute("id").Value)).Select(mn => mn.Attribute("href").Value).ToList();
                _currentPage = 0;
                bookFileNames = _menuItems.ToArray();
                string uri = GetPath(0);
                string annotFileName = fileName + ".xml";
                if (File.Exists(annotFileName))
                    bookAnnotes = XDocument.Load(fileName + ".xml");
                else
                    bookAnnotes = createEmptyAnnotDocument();
                currentAnnotes = bookAnnotes;
                isBookAnnotes = checkCurrentAnnotesType();
                
                annotesByChapter=divideAnnotesByChapter();
                browsingFinish = annotesByChapter.Length - 1;
                fullBookText = new List<String>[annotesByChapter.Length];
                epubDisplay.Navigate(uri);
                NextButton.Visibility = Visibility.Visible;
            }
        }

        private bool checkCurrentAnnotesType ()
        {
            if (currentAnnotes.Root.Attribute("type").Value=="book")
                return true;
            else
                return false;
        }

        private bool isElementDoubt (XElement el)
        {
            if (el.Element("category").Value=="Doubt")
                return true;
            else
                return false;
        }

        //public void loadAnnotations(string filename);

        public MemoryStream ConvertToMemmoryStream(string fillPath)
        {
            var xml = File.ReadAllText(fillPath);
            byte[] encodedString = Encoding.UTF8.GetBytes(xml);

            // Put the byte array into a stream and rewind it to the beginning
            MemoryStream ms = new MemoryStream(encodedString);
            ms.Flush();
            ms.Position = 0;

            return ms;
        }

        public string GetPath(int index)
        {
             string result = String.Format("file:///{0}", Path.GetFullPath(Path.Combine(_tempPath, _baseMenuXmlDiretory, _menuItems[index])));
            return result;
        }

        public string getFileName(int index)
        {
            //return Path.Combine(_baseMenuXmlDiretory,_menuItems[index]);
            return _baseMenuXmlDiretory + '/' + _menuItems[index];
        }

        public int getCurrentPage()
        {
            return _currentPage;
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _menuItems.Count - 1)
            {
                _currentPage++;
            }
            else
            {
                NextButton.Visibility = Visibility.Hidden;
            }
            if (_currentPage == _menuItems.Count - 1)
            {
                NextButton.Visibility = Visibility.Hidden;
            }
            if (_currentPage > 0)
            {

                PreviousButton.Visibility = Visibility.Visible;
            }
            NavigateToPage(_currentPage);
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage >= 1)
            {
                _currentPage--;
            }
            else
            {

                PreviousButton.Visibility = Visibility.Hidden;
            }
            if (_currentPage == 1)
            {
                PreviousButton.Visibility = Visibility.Hidden;
            }
            if (_currentPage <= _menuItems.Count - 1)
            {
                NextButton.Visibility = Visibility.Visible;
            }
            NavigateToPage(_currentPage);
        }

        private void NavigateToPage (int i)
        {
            pageLoaded = false;
            epubDisplay.Navigate(GetPath(i));
        }

        /*private void loadExclusions()
        {
            XDocument doc = XDocument.Load(currentLanguage + ".ex");
            IEnumerable<XElement> list = doc.Root.Elements("word");
            exclusions = new string[list.Count()];
            int i = 0;
            foreach (XElement el in list)
            {
                exclusions[i] = el.Value;
                i++;
            }
        } */

        private List<string> getListOfWords(XDocument doc)
        {
            List<string> l = doc.Descendants("word")
                .Select(item => item.Value).
                ToList();
            return l;
        }
        

        private async void BrowseBookAndSearch()
        {
            inBrowseMode = true;
            searchOn = false;
            for (currentlyBrowsing = browsingStart; currentlyBrowsing <= browsingFinish; currentlyBrowsing++)
                if (fullBookText[currentlyBrowsing] == null)
                {
                    NavigateToPage(currentlyBrowsing);
                    await PageLoad();
                }
            inBrowseMode = false;
            //NavigateToPage(_currentPage);
            searcher.RunWorkerAsync();
            
        }

        private SearchWindow getSearchWindow(string l)
        {
            SearchWindow result = new SearchWindow(l);
            result.SearchConfirmed += (s, e) =>
            {
                searchItem = result.TextQuery;
                searchOn = (searchItem != "");
                if (searchOn)
                    certainTextSearch(searchItem);
                else
                {
                    exclusions = getListOfWords(excFile);
                    if (searchWindow.IsNoun)
                        exclusions.AddRange(getListOfWords(verbsFile));
                    else
                        exclusions.AddRange(getListOfWords(nounsFile));
                    mainQuery = result.CertainQuery;
                    doubtQuery = result.DoubtQuery;
                    BrowseBookAndSearch();
                    
                }



            };
            return result;
        }

        private List<string> getTextFromPage (List<IHTMLElement> webPage)
        {
            List<string> result = new List<string>();

            foreach (IHTMLElement element in webPage)
                result.Add(element.innerText);
            return result;

        }



        

        private async Task PageLoad()
        {
            TaskCompletionSource<bool> PageLoaded = null;
            PageLoaded = new TaskCompletionSource<bool>();
            epubDisplay.LoadCompleted += (s, e) =>
            {
                if (!pageLoaded) return; //if not complete
                if (PageLoaded.Task.IsCompleted) return;
                PageLoaded.SetResult(true);
            };

            while (PageLoaded.Task.Status != TaskStatus.RanToCompletion)
            {
                await Task.Delay(10);

            }
        }

        private void OpenEditWindow (object sender, RoutedEventArgs e)
        {
            editWindow.SetChapter(initialColl, annotesByChapter[_currentPage]);
            editWindow.initVariables(AnnotBox.SelectedIndex);
            editWindow.Visibility = Visibility.Visible;
        }

        private void DeleteAnnot (object sender, RoutedEventArgs e)
        {
            int annotNr;
            XElement annotationToDelete;
            annotNr = AnnotBox.SelectedIndex;
            annotationToDelete = annotesByChapter[_currentPage].ElementAt(annotNr);
            annotesByChapter[_currentPage].RemoveAt(annotNr);
            //highlightNote(annotationToDelete, "#FFFFFF");
            epubDisplay.Navigate(GetPath(_currentPage));
            markAnnotations(docOrg);
            //docOrg.
        }

        public bool areFilesSaved()
        {
            return true;
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            //saveAnnotesToFile("kevade.xml");
            nounsFile.Save(bookData[2]+".noun");
            verbsFile.Save(bookData[2] + ".verb");
            excFile.Save(bookData[2] + ".ex");
            Close();
        }

        private void EpubDisplay_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void highlightRange(IHTMLTxtRange range, string color)
        {
            range.execCommand("BackColor", false, color);
        }

        private string getTrimmedSelectionText()
        {
           IHTMLTxtRange range = getRange();
            if (range == null)
                return null;
            return (range.text).Trim();
            
        }

        

        private List<XElement> copyAnnotations (List<XElement> sourceList)
        {
            List<XElement> result = new List<XElement>();
            foreach (XElement el in sourceList)
                result.Add(new XElement(el));
            return result;
        }

        private IHTMLTxtRange getRangeFromAnnotation (XElement el, IHTMLTxtRange range = null)
        {
            int tagNr, startWord, fragmentLen;
            IHTMLElement tag;
            string text;
            tagNr = (int)el.Element("data").Element("tagelement");
            startWord = (int)el.Element("data").Element("startfragment") - 1;
            fragmentLen = (int)el.Element("data").Element("numberofwords");
            text = (string)el.Element("text");
            tag = initialColl[tagNr];
            if (range==null)
                range = getRange();
            range.moveToElementText(tag);
            range.collapse();
            range.moveStart("word", startWord);
            range.moveEnd("word", fragmentLen);
            return range;
        }

        private IHTMLTxtRange compareRanges (int elementNr, IHTMLTxtRange range)
        {
            IEnumerable<XElement> setAnnotes;
            XElement root = currentAnnotes.Root;
            IHTMLTxtRange rangeCheck = getRange();
            //int annotNr;
            string bookFile = getFileName(_currentPage);
            setAnnotes =
            from el in root.Elements("annotation")
            where (((string)el.Element("data").Element("filename") == bookFile) && ((int)el.Element("data").Element("tagelement") == elementNr))
            select el;
            foreach (XElement el in setAnnotes)
            {
                rangeCheck = getRangeFromAnnotation(el,rangeCheck);
                if (rangeCheck.inRange(range))
                {
                    //annotNr = ch
                    //get elNr from List and return it
                    
                    return rangeCheck;
                }
               
            }
            return range;
        }

        private string highlightNote(XElement el, string color, IHTMLTxtRange range = null)
        {
            int tagNr, startWord, fragmentLen;
            IHTMLElement tag;
            string text;
            tagNr = (int)el.Element("data").Element("tagelement");
            startWord = (int)el.Element("data").Element("startfragment") - 1;
            fragmentLen = (int)el.Element("data").Element("numberofwords");
            text = (string)el.Element("text");
            tag = initialColl[tagNr];
            if (range == null)
                range = getRange();
            range.moveToElementText(tag);
            range.collapse();
            range.moveStart("word", startWord);
            range.moveEnd("word", fragmentLen);
            if (color == "#000000")
                range.execCommand("removeFormat", false, null);
            else
                range.execCommand("BackColor", false, color);
            return range.text;
        }

        private List<XElement>[] divideAnnotesByChapter ()
        {
            int num = _menuItems.Count;
            IEnumerable<XElement> chAnnotes;
            List<XElement> chAnnotesList;
            string bookFile = null;
            List<XElement>[] listByChapter = new List<XElement>[num];
            XElement root = currentAnnotes.Root;
            for (int i = 0; i<num; i++)
            {
                bookFile = getFileName(i);
                XElement r = currentAnnotes.Root;
                chAnnotes =
                from el in r.Elements("annotation")
                orderby (int)el.Element("data").Element("tagelement"), (int)el.Element("data").Element("startfragment")
                where (string)el.Element("data").Element("filename") == bookFile
                select el;
                chAnnotesList = chAnnotes.ToList();
                listByChapter[i]=chAnnotesList;
                
            }
            return listByChapter;
        }

        private List<IHTMLElement> getListOfElements(IHTMLDocument2 document, string tag = "")
        {
            IHTMLElement e;
            IHTMLElementCollection coll = document.all;
            List<IHTMLElement> fullList = new List<IHTMLElement>();

            for (int i = 0; i < coll.length; i++)
            {
                e = coll.item(i);
                if (e.tagName==tag || tag=="")
                    fullList.Add(e);

            }
            return fullList;
        }

        private void markAnnotations(IHTMLDocument2 document)
        {
            string bookFile = getFileName(_currentPage);
            int tagNr, startWord, fragmentLen;
            string text;
            string color;
            bool doubt;

            AnnotBox.Items.Clear();
            //_listBoxPrevInd = -1;
             //IHTMLElement tag;
             IHTMLSelectionObject currentSelection=document.selection;
             IHTMLTxtRange range = currentSelection.createRange() as IHTMLTxtRange;
             /*XElement root = bookAnnotes.Root;
                 chapterAnnotes =
                 from el in root.Elements("annotation")
                 where (string)el.Element("data").Element("filename") == bookFile
                 select el;
            List<XElement> chAnnotList = chapterAnnotes.ToList();
            List<XElement> chAnnotOriginal = new List<XElement>(chAnnotList); */
             foreach (XElement el in annotesByChapter[_currentPage])
             {
                /*tagNr = (int)el.Element("data").Element("tagelement");
                startWord = (int)el.Element("data").Element("startfragment")-1;
                fragmentLen = (int)el.Element("data").Element("numberofwords");
                text = (string)el.Element("text");
                tag = initialColl[tagNr];
                range.moveToElementText(tag);
                range.collapse();
                range.moveStart("word", startWord);
                range.moveEnd("word", fragmentLen);
                range.execCommand("BackColor", false, "FFFF00"); */
                range = getRangeFromAnnotation(el, range);
                if (!isBookAnnotes && isElementDoubt(el))
                {
                    text = "?" + range.text;
                    color = "0398FC";

                }
                else
                {
                    text = range.text;
                    color = "FFFF00";
                }
                    highlightRange(range, color);
                //AnnotBox.Items.Add(range.text);
                AnnotBox.Items.Add(text);

             } 


            //coll = document.all;
            /*IHTMLElement el = c.item(70);
            IHTMLSelectionObject currentSelection = document.selection;
            IHTMLTxtRange range = currentSelection.createRange() as IHTMLTxtRange;
            range.moveToElementText(el);
            range.collapsed
            range.moveStart("word", 7);
            range.moveEnd("word", 1);
            range.execCommand("BackColor", false, "FFFF00"); */
            //Console.WriteLine();

        }

        private void updateXML(List<XElement> oldList, List<XElement> newList)
        {
            int ind = 0;
            foreach (XElement element in oldList)
            {
                element.ReplaceWith(newList[ind]);
                ind++;
            }
        }

        private IHTMLTxtRange getRange()
        {
            IHTMLDocument2 document = epubDisplay.Document as IHTMLDocument2;
            IHTMLSelectionObject currentSelection = document.selection;
            IHTMLTxtRange range = currentSelection.createRange() as IHTMLTxtRange;
            return range;

        }

        private void changeCharset(IHTMLDocument2 doc, string charset)
        {
            doc.charset = charset;
            epubDisplay.Refresh();
        }

        private void EpubDisplay_LoadCompleted(object sender, NavigationEventArgs e)
        {
            
            IHTMLDocument2 document = epubDisplay.Document as IHTMLDocument2;
            if (document.charset != "utf-8")
                changeCharset(document, "utf-8");
            int pageIndex;
            if (inBrowseMode)
                pageIndex = currentlyBrowsing;
            else
                pageIndex = _currentPage;
            initialColl = getListOfElements(document, "P");
            //docOrg = epubDisplay.Document as IHTMLDocument2;
            if (fullBookText[pageIndex] == null)
            {
                
                fullBookText[pageIndex] = getTextFromPage(initialColl);
            }
            
            if (!inBrowseMode&&annotationsShown)
            {
                _listBoxPrevInd = -1;
                markAnnotations(document);
                _listBoxPrevInd = -1;
                
            }
            if (searchOn)
                certainTextSearch(searchItem);
            pageLoaded =true;
            
        }

        

        private void AnnotBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            IHTMLTxtRange range;
            int ind = (sender as ListBox).SelectedIndex;
            int tagId;
            XElement el = annotesByChapter[_currentPage].ElementAt(ind);
            tagId = (int)el.Element("data").Element("tagelement");
            if (ind > -1)
            {
                range = getRangeFromAnnotation(el, getRange());
                range.select();
                initialColl[tagId].scrollIntoView();
            }

                /*if (_listBoxPrevInd>-1)
                {
                    range = getRangeFromAnnotation(chapterAnnotes.ElementAt(_listBoxPrevInd),getRange());
                    highlightRange(range, "FFFF00");
                    //highlightNote(chapterAnnotes.ElementAt(_listBoxPrevInd), getRange(), "FFFF00");
                }
                int ind = (sender as ListBox).SelectedIndex;
                if (ind > -1)
                {
                    range = getRangeFromAnnotation(chapterAnnotes.ElementAt(ind), getRange());
                    highlightRange(range,"FFFF00");
                    _listBoxPrevInd = ind;
                    initialColl[ind].scrollIntoView(); 
                } */

            }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {

        }

        private void TestSearch(object sender, RoutedEventArgs e)
        {
            //BrowseBook();
            searchWindow.Show(); 
        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e) //OpenAnnotation
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                currentAnnotes = XDocument.Load(filePath);
                isBookAnnotes = checkCurrentAnnotesType();
                annotesByChapter = divideAnnotesByChapter();
                NavigateToPage(_currentPage);
            }
        }

        private void AddNoun(object sender, RoutedEventArgs e)
        {
            addWordsToLanguageFile(nounsFile, getTrimmedSelectionText());
        }

        private void AddVerb(object sender, RoutedEventArgs e)
        {
            addWordsToLanguageFile(verbsFile, getTrimmedSelectionText());
        }

        private void AddExclusion(object sender, RoutedEventArgs e)
        {
            addWordsToLanguageFile(excFile, getTrimmedSelectionText());
        }

        private void MenuItem_Click_3(object sender, RoutedEventArgs e) //Save Annotations
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*";
            if (saveFileDialog.ShowDialog() == true)
            {
                string filePath = saveFileDialog.FileName;
                currentAnnotes.Save(filePath);
            }

        }

        private void MenuItem_Click_4(object sender, RoutedEventArgs e) //Add annotes from file
        {

        }

        private void MenuItem_Click_2(object sender, RoutedEventArgs e) //Show Hide Marks
        {
            annotationsShown = !annotationsShown;
            if (!annotationsShown)
                AnnotBox.Items.Clear();
            NavigateToPage(_currentPage);
        }
    }
}
