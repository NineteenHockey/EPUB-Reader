using Microsoft.Win32;
using EpubReaderWithAnnotations.Utilities;
using System;
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
        private static XElement annotCategories;
        private static string[][] mainCategories;
        private static string[][] subCategories;
        private static AnnotationEdit editWindow = new AnnotationEdit();
        private static BookData book;
        private BrowserOperations browser;
        private string _tempPath; // library dir
        private string _baseMenuXmlDiretory;
        private List<string> _menuItems; //book chapters?
        private int _currentPage;
        private XDocument bookAnnotes;
        private List<XElement>[] annotesByChapter;
        private IEnumerable<XElement> chapterAnnotes;
        private List<IHTMLElement> initialColl;
        private int _listBoxPrevInd = -1;
        private HTMLDocumentEvents2_Event _docEvent;
        private IHTMLDocument2 docOrg;
        bool isAssigned = false;

        public MainWindow()
        {
            InitializeComponent();
            epubDisplay.LoadCompleted += delegate
            {
                if (_docEvent != null /* && !isAssigned -- not needed?? */)
                {
                    _docEvent.ondblclick -= _docEvent_ondblclick;

                }
                if (epubDisplay.Document != null)
                {
                    _docEvent = (HTMLDocumentEvents2_Event)epubDisplay.Document;
                    _docEvent.ondblclick += _docEvent_ondblclick;
                    
                }
            };

            editWindow.AnnotationChangesConfirmed += (s, e) =>
            {
                annotesByChapter[_currentPage] = editWindow.NewAnnotes;
                epubDisplay.Navigate(GetPath(_currentPage));
            };
            _menuItems = new List<string>();//book chapters?
            NextButton.Visibility = Visibility.Hidden;
            PreviousButton.Visibility = Visibility.Hidden;
            annotCategories = XElement.Load("categories.xml");
            mainCategories = readCategoryList();
            subCategories = createSubCategriesArray();
            //string[] subCats = getSubCategories("Dictionary");
            //int i = 444;
            
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
                var menuItemsIds = menuReader.Root.Element(ns + "spine").Descendants().Select(x => x.Attribute("idref").Value).ToList();
                _menuItems = menuReader.Root.Element(ns + "manifest").Descendants().Where(mn => menuItemsIds.Contains(mn.Attribute("id").Value)).Select(mn => mn.Attribute("href").Value).ToList();
                _currentPage = 0;
                string uri = GetPath(0);
                bookAnnotes = XDocument.Load("Kevade.xml");
                annotesByChapter=divideAnnotesByChapter();
                epubDisplay.Navigate(uri);
                NextButton.Visibility = Visibility.Visible;
            }
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
            return String.Format("file:///{0}", Path.GetFullPath(Path.Combine(_tempPath, _baseMenuXmlDiretory, _menuItems[index])));
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
            string uri = GetPath(_currentPage);
            epubDisplay.Navigate(uri);
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
            string uri = GetPath(_currentPage);
            epubDisplay.Navigate(uri);
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

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            saveAnnotesToFile("kevade.xml");
            Close();
        }

        private void EpubDisplay_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void highlightRange(IHTMLTxtRange range, string color)
        {
            range.execCommand("BackColor", false, color);
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
            XElement root = bookAnnotes.Root;
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
            XElement root = bookAnnotes.Root;
            for (int i = 0; i<num; i++)
            {
                bookFile = getFileName(i);
                XElement r = bookAnnotes.Root;
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

        private List<IHTMLElement> getListOfElements(IHTMLDocument2 document)
        {
            IHTMLElement e;
            IHTMLElementCollection coll = document.all;
            List<IHTMLElement> fullList = new List<IHTMLElement>();

            for (int i = 0; i < coll.length; i++)
            {
                e = coll.item(i);
                fullList.Add(e);

            }
            return fullList;
        }

        private void markAnnotations(IHTMLDocument2 document)
        {
            string bookFile = getFileName(_currentPage);
            int tagNr, startWord, fragmentLen;
            string text;

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
                highlightRange(range, "FFFF00");
                AnnotBox.Items.Add(range.text);

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

        

        private void EpubDisplay_LoadCompleted(object sender, NavigationEventArgs e)
        {
            
            IHTMLDocument2 document = epubDisplay.Document as IHTMLDocument2;
            docOrg = epubDisplay.Document as IHTMLDocument2;
            initialColl = getListOfElements(document);
            _listBoxPrevInd = -1;
            markAnnotations(document);
            _listBoxPrevInd = -1;
            
        }

        

        private void AnnotBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            IHTMLTxtRange range;
            int ind = (sender as ListBox).SelectedIndex;
            if (ind > -1)
            {
                range = getRangeFromAnnotation(annotesByChapter[_currentPage].ElementAt(ind), getRange());
                range.select();
                initialColl[ind].scrollIntoView();
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
    }
}
