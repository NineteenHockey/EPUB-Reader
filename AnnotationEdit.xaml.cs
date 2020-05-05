using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;
using mshtml;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace EpubReaderWithAnnotations
{
    /// <summary>
    /// Interaction logic for AnnotationEdit.xaml
    /// </summary>
    ///  
    class SingleAnnotation
    {
        XElement el;//Maybe adding XElement as a field?
        string addedDate;// - not used, adding updated date maybe?
        string headword;// to get all comments from one word
        string text; //text of annotation
        string type; // more general classification of annotation
        string category; // sub type of the annotation
        string comment; //right now just text, maybe HTML later
        //data block comes next
        string filename; //probably not used and never changed
        int tagElement; //number of the tag in the html file
        int startFragment; //number of first word in annotation. 0 or 1 indexed?
        int numberOfWords;

        public const byte HEADWORD = 0;
        public const byte TEXT = 1;
        public const byte TYPE = 2;
        public const byte CATEGORY = 3;
        public const byte COMMENT = 4;
        public const byte TAG_ELEMENT = 5;
        public const byte START_FRAGMENT = 6;
        public const byte NUMBER_OF_WORDS = 7;

        public const byte ARRAY_LENGTH=8;

        public SingleAnnotation(XElement element)
        {
            //addedDate = (string)element.Element("addeddate");
            headword = (string)element.Element("headword");
            text = (string)element.Element("text");
            type = (string)element.Element("type");
            category = (string)element.Element("category");
            comment = (string)element.Element("comment");
            //filename
            tagElement = (int)element.Element("data").Element("tagelement");
            startFragment = (int)element.Element("data").Element("startfragment");
            numberOfWords = (int)element.Element("data").Element("numberofwords");
            el = element;
        }

        public SingleAnnotation(string path)
        {
            XElement initElement =
                new XElement("annotation",
                    new XElement("addeddate", System.DateTime.Now),
                    new XElement("updateddate"),
                    new XElement("headword"),
                    new XElement("text"),
                    new XElement("type"),
                    new XElement("category"),
                    new XElement("comment"),
                    new XElement("data",
                        new XElement("filename", path),
                        new XElement("tagelement"),
                        new XElement("startfragment"),
                        new XElement("numberofwords")
                    )
                );
            el = initElement;
        }
    }
    public partial class AnnotationEdit : Window
    {
        public event EventHandler AnnotationChangesConfirmed;

        List<IHTMLElement> chapterTags; // List of all HtmlTags
        List<XElement> currentNotes, updatedNotes; // Original and changed list of annotations
        public List<XElement> NewAnnotes { get { return updatedNotes; } }
        int noteIndex = -1; //Current annotation index
        int startNoteOfCurrentTag = -1;
        int finishNoteOfCurrentTag = -1;
        int chapterElementNr = -1;
        int underlinedElement = -1;
        string chapterPath;
        string[] tagsTextDividedIntoWords;
        const string neutralBackground = "#FFFFFF";
        bool inAnnotationMode = true; //if false then mode is words
        bool inForwardDirection = true;
        int textLength;
        XElement currentNote;
        string[][] AnnotTypeItems;
        string[][] AnnotCategoriesItems;
        BrushConverter bc = new BrushConverter();
        SingleAnnotation currentAnnotation = null;
        bool updateInProgress = false;
        int newStart;
        int newLength;

        public AnnotationEdit()
        {
            InitializeComponent();
            //commentsClassification = MainWindow.getCategoryList();
            //AnnotationType.ItemsSource = commentsClassification[0];
            this.Visibility = Visibility.Hidden;
            
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            updateInProgress = false;
            this.Visibility = Visibility.Hidden;
            e.Cancel = true;
            
        }

        private void SetLinearGradientUnderline()
        {
            // Create an underline text decoration. Default is underline.
            TextDecoration myUnderline = new TextDecoration();

            // Create a linear gradient pen for the text decoration.
            Pen myPen = new Pen();
            myPen.Brush = new LinearGradientBrush(Colors.Yellow, Colors.Red, new Point(0, 0.5), new Point(1, 0.5));
            myPen.Brush.Opacity = 0.5;
            myPen.Thickness = 1.5;
            myPen.DashStyle = DashStyles.Dash;
            myUnderline.Pen = myPen;
            myUnderline.PenThicknessUnit = TextDecorationUnit.FontRecommended;

            // Set the underline decoration to a TextDecorationCollection and add it to the text block.
            TextDecorationCollection myCollection = new TextDecorationCollection();
            myCollection.Add(myUnderline);
            AnnotText.TextDecorations = myCollection;
        }
        
        public void initVariables(int i) //showing the window again
        {
            noteIndex = i;
            inForwardDirection = inAnnotationMode = true;
            showChapterElement();
        }
        
        private string getAnnotationText()
        {
            return null;
        }

        private void startUpdate(XElement element)
        {
            updateInProgress = true;    
            currentAnnotation = new SingleAnnotation(element);
            newStart = (int)element.Element("data").Element("startfragment");
            newLength = (int)element.Element("data").Element("numberofwords");
        }

        private string[] getDataFromAnnotationWindow ()
        {
            string[] array = new string[SingleAnnotation.ARRAY_LENGTH];
            array[SingleAnnotation.HEADWORD] = AnnotationHeadword.Text;
            //array[SingleAnnotation.TEXT] = "";
            array[SingleAnnotation.TYPE] = AnnotationType.SelectedItem.ToString();
            array[SingleAnnotation.CATEGORY] = AnnotationCategory.SelectedItem.ToString();
            array[SingleAnnotation.COMMENT] = AnnotationComment.Text;
            //array[SingleAnnotation.TAG_ELEMENT] = 0;//tagEle=0;
            //array[SingleAnnotation.START_FRAGMENT] = 0;//StartFragment=0;
            //array[SingleAnnotation.NUMBER_OF_WORDS] = 0;//NumberOfWords=0;

            return array;
        }

        private void fillForm (XElement element)
        {
            AnnotationType.SelectedItem =(string)element.Element("type");
            AnnotationType.ScrollIntoView(AnnotationType.SelectedItem);
            AnnotationHeadword.Text = (string)element.Element("headword");
            AnnotationCategory.SelectedItem = (string)element.Element("category");
            AnnotationCategory.ScrollIntoView(AnnotationCategory.SelectedItem);
            AnnotationComment.Text = (string)element.Element("comment");
        }

        private string getAnnotationText (string[] words, int start, int length)
        {
            StringBuilder sb = new StringBuilder("");
            start--; //switching from 1-based to 0-based index
            for (int i = start; i<start+length; i++)
            {
                if (i> start)
                    sb.Append(" ");
                sb.Append(words[i]);
            }
            return sb.ToString();


        }

        private void addNewNodeIfNotPresent (XElement element, string s)
        {
            if (element.Element(s) == null)
                element.Add(new XElement(s));
                
        }

        private void saveUpdatedInfo(XElement element)
        {
            string type;
            string category;
            if (AnnotationType.SelectedItem != null)
                type = AnnotationType.SelectedItem.ToString();
            else
                type = "";
            if (AnnotationCategory.SelectedItem != null)
                category = AnnotationCategory.SelectedItem.ToString();
            else
                category = "";
            addNewNodeIfNotPresent(element, "headword");
            addNewNodeIfNotPresent(element, "type");
            addNewNodeIfNotPresent(element, "category");
            addNewNodeIfNotPresent(element.Element("data"), "updateddate");
            element.Element("type").Value = type;
            element.Element("headword").Value = AnnotationHeadword.Text;
            element.Element("category").Value = category;
            element.Element("text").Value = getAnnotationText(tagsTextDividedIntoWords, newStart, newLength);
            element.Element("comment").Value = AnnotationComment.Text;
            element.Element("data").Element("updateddate").Value = System.DateTime.Now.ToString();
            element.Element("data").Element("startfragment").Value = newStart.ToString();
            element.Element("data").Element("numberofwords").Value = newLength.ToString();
            updateInProgress = false;

        }

        private void enableEdit(bool on)
        {
            AnnotationType.IsEnabled = on;
            AnnotationHeadword.IsEnabled = on;
            AnnotationCategory.IsEnabled = on;
            AnnotationComment.IsEnabled = on;
        }

        private void showChapterElement(int start = -1, int finish = -1) //set start and finish notes of current tag, change chapter element, show text
        {
            bool newChapterElement = true;
            if (AnnotTypeItems == null) //finishing initialization. I guess it is wrong
            {
                AnnotTypeItems = MainWindow.getCategoryArray();
                AnnotCategoriesItems = MainWindow.getSubCategoryArray();
                AnnotationType.ItemsSource = AnnotTypeItems[0];
                //AnnotationType.ScrollIntoView("Dictionary");
                //AnnotationType.SelectedItem="Dictionary";
                
            }

            if (start < 0) {
                startNoteOfCurrentTag = findInitialNote(noteIndex);
                newChapterElement = false;
            }
            else
                startNoteOfCurrentTag = start;
            if (finish < 0) {
                finishNoteOfCurrentTag = findEndNote(noteIndex);
                newChapterElement = false;
            }
            else
                finishNoteOfCurrentTag = finish;
            //if (newChapterElement || !inAnnotationMode)
            //    changeChapterElement();
            initChapterElement();
            showText();
        }

       /* private void showChapterElement () //move to previous or next chapter element if there is no annotation
        {
            //int testNoteIndex;
            if (!inAnnotationMode)
                if (inForwardDirection)
                    noteIndex++;
                else
                    noteIndex--;
        } */

       

        private void initChapterElement()
        {
            tagsTextDividedIntoWords = textIntoWords(chapterTags.ElementAt(chapterElementNr).innerText);
            textLength = tagsTextDividedIntoWords.Length;
            if (inAnnotationMode)
            {
                currentNote = updatedNotes.ElementAt(noteIndex);
                fillForm(currentNote);
                underlinedElement = (int)currentNote.Element("data").Element("startfragment") - 1;
            }
            else
            {
                if (inForwardDirection)
                    underlinedElement = 0;
                else
                { 
                    underlinedElement = textLength-1;
                    XElement note = updatedNotes.ElementAt(finishNoteOfCurrentTag);
                    int startWord = (int)note.Element("data").Element("startfragment") - 1;
                    int numOfWords = (int)note.Element("data").Element("numberofwords");
                    int endWord = startWord + numOfWords - 1;
                    if (endWord == textLength - 1)
                        underlinedElement = startWord;
                    else
                        underlinedElement = textLength - 1;
                    //moveMarkToAnnotation();
                }
                    
            }
            AnnotText.Inlines.Clear();
        }



        private void changeChapterElement()
        //if !annot - change chapterElement then get new text if annot set currentNote and underlinedElement 
        //else set underlinedElement. Clear text in textbox. 
        {
            /*if (!annotation)
            { */
                if (inForwardDirection)
                {
                    chapterElementNr++;
                }
                else
                {
                    chapterElementNr--;
                }
            //}
        }

        private int getTagNumber(int i)
        {
            return (int) updatedNotes.ElementAt(i).Element("data").Element("tagelement");
        }

        private int findInitialNote(int i)
        {
            //int elNr;
            if (inAnnotationMode)
                chapterElementNr = getTagNumber(i);
            

            for (int j=i-1; j>-1; j--)
            {
                if (getTagNumber(j) != chapterElementNr)
                    return j + 1;

            }
            return 0;
               
        }

        private int findEndNote(int i)
        {
            

            while (i<=updatedNotes.Count)
            {
                i++;
                if (getTagNumber(i) != chapterElementNr)
                    return i - 1;
            }
            return -1;
        }

     

        private string [] textIntoWords (string s)
        {
            //string regExExpression = @"([\p{L}’]+)|([\p{P}]+)";
            string regExExpression = @"([\p{L}’]+\s*)|([\p{P}]+\s*)";
            int ind = 0;
            MatchCollection mc = Regex.Matches(s, regExExpression);
            string[] result = new string[mc.Count];
            foreach (Match match in mc)
            {
                result[ind] = match.Value;
                ind++;
            }
            return result;
            //return Regex.Split(s, regExExpression);
            //return s.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
        }

        public void SetChapter(List<IHTMLElement> elements, List<XElement> notes)
        {
            chapterTags = elements;
            currentNotes = notes; 
            updatedNotes = copyAnnotations(currentNotes);
            chapterPath = (string)notes[0].Element("data").Element("filename");
            //SetLinearGradientUnderline();
            //showText();
        }

        private void removeUnderline()
        {
            Inline inl;
            //TextDecorationCollection tdc;
            for (int i = underlinedElement; i<AnnotText.Inlines.Count; i++)
            {
                inl = AnnotText.Inlines.ElementAt(i);
                if (inl.TextDecorations!=null && inl.TextDecorations.Count > 0)
                    inl.TextDecorations=null;
                else
                    break;
            }
            
            
        }

        private Run NewWordToTextblock(string word, string backgournd, bool underlined)
        {
            Run r = new Run(word)
            {
                Background = (Brush)bc.ConvertFrom(backgournd)
            };
            if (underlined)
            {
                r.TextDecorations = TextDecorations.Baseline;
            }
            return r;

        }

        

        private void printText(string[] words, int firstWord, int numberOfWords, string background = neutralBackground)
        {
            int underlinedCount;
            //ref Inline olditem;
            bool noteHighlight;
            Inline inl;
            //TextDecorations.underlinedElement;
            //TextDecorations.underlinedElement;
            //TextDecorations = null;
            if (background == neutralBackground)
                noteHighlight = false;
            else
                noteHighlight = true;
            //if (underlinedElement > -1)
            //    removeUnderline();
           // underlinedElementdElement = underlinedElement;

            int nextArea = firstWord + numberOfWords;
            underlinedCount = underlinedElement;
            for (int i = firstWord; i < nextArea; i++)
            {
                AnnotText.Inlines.Add(NewWordToTextblock(words[i], background, underlinedCount==i));
                if (noteHighlight && underlinedElement > -1)
                    underlinedCount++;


            }

        }

        private void changeHighlight (string[] words, int index, string background)
        {
            index--; //correcting the fact that in the file counting starts from 1 and in the array from 0;=
            Inline current, neighbour;
            bool underlined = (background!=neutralBackground);
            int neighbourInt;
            if (index == 0)
                neighbourInt = 1;
            else
                neighbourInt = index - 1;
                
            current = AnnotText.Inlines.ElementAt(index);
            neighbour = AnnotText.Inlines.ElementAt(neighbourInt);
            AnnotText.Inlines.Remove(current);
            if (index == 0)
                AnnotText.Inlines.InsertBefore(neighbour, NewWordToTextblock(words[index], background, underlined));
            else
                AnnotText.Inlines.InsertAfter(neighbour, NewWordToTextblock(words[index], background, underlined));
        }

        private void showText3 ()
        {
            AnnotText.Text = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum. Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum. Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum. Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.";
        }

        private void showText() // method to print all the text in the chapterElement
        {
            int startWord;
            int numOfWords;
            int nextWord=0; //from which word to start next print;
            XElement note = updatedNotes.ElementAt(noteIndex);
            int curNr = getTagNumber(noteIndex);
            if (getTagNumber(noteIndex)!=chapterElementNr)
            {
                printText(tagsTextDividedIntoWords, 0, tagsTextDividedIntoWords.Length);
                return;
            }
            //int tagNr = (int)note.Element("data").Element("tagelement");
            //AnnotText.Text = chapterTags.ElementAt(tagNr).innerText;
            
            for (int i = startNoteOfCurrentTag; i<=finishNoteOfCurrentTag; i++)
            {
                note = updatedNotes.ElementAt(i);
                startWord = (int)note.Element("data").Element("startfragment")-1;
                numOfWords = (int)note.Element("data").Element("numberofwords");
                
                printText(tagsTextDividedIntoWords, nextWord, startWord - nextWord);
                printText(tagsTextDividedIntoWords, startWord, numOfWords,"#FFFF00");
                nextWord = startWord + numOfWords;

            }
            printText(tagsTextDividedIntoWords, nextWord, tagsTextDividedIntoWords.Length-nextWord);

            
        }

        private List<XElement> copyAnnotations(List<XElement> sourceList)
        {
            List<XElement> result = new List<XElement>();
            foreach (XElement el in sourceList)
                result.Add(new XElement(el));
            return result;
        }

        private void moveCursor(int start, int len = 1)
        {
            Inline inl=null;
            if (inForwardDirection)
                for (int i = start; i < textLength-1; i++)
                {
                    inl = AnnotText.Inlines.ElementAt(i);
                    if (inl.TextDecorations != null && inl.TextDecorations.Count > 0)
                        start++;
                    else
                        break;
                }

            if (start >= textLength)
            {
                //WordForward.IsEnabled = false;
                return;
            }
            removeUnderline();
            underlinedElement = start;
            for (int i = start; i < start + len; i++)
            {
                inl = AnnotText.Inlines.ElementAt(i);
                inl.TextDecorations = TextDecorations.Baseline;
                
            }
            double leftCoordinate = inl.ElementStart.GetCharacterRect(LogicalDirection.Forward).Top;
            AnnotTextViewer.ScrollToVerticalOffset(leftCoordinate);
            
            //AnnotTextViewer.
            
        }

        private void underlineWord(int start, int len = 1)
        {
            /*int finish = start + len;
            if (len<0)
            {s
                finish = start;
                start = start + len;
            } */
            
            Inline inl;
            if ((inForwardDirection && start > textLength - 1) || (!inForwardDirection && start < 0))
            {
                
                changeChapterElement();
                if (inForwardDirection)
                    noteIndex++;
                else
                    noteIndex--;
                showChapterElement();
                //showText();
                return;
            }

            if (AnnotText.Inlines.ElementAt(start).Background!=(Brush)bc.ConvertFrom(neutralBackground)) //if we move by annot
            {
                if (inForwardDirection && inAnnotationMode)
                    MoveToNextAnnotation();
                else if (inAnnotationMode)
                {
                    //MoveToPreviousAnnotation();
                    moveMarkToAnnotation(updatedNotes[noteIndex]);
                    return;
                }
                else if (!inAnnotationMode)
                    if (inForwardDirection)
                        noteIndex++;
                    else
                        noteIndex--;
            }
            moveCursor(start, len);// move to next cursor free word
            /*for (int i = start; i<textLength; i++)
                {
                    inl = AnnotText.Inlines.ElementAt(i);
                    if (inl.TextDecorations!=null && inl.TextDecorations.Count > 0)
                        start++;
                    else
                        break;
                }
            
            if (start >= textLength)
            {
                //WordForward.IsEnabled = false;
                changeChapterElement(annotation, forward);
                return;
            }
            removeUnderline();
            underlinedElement = start;
            for (int i = start; i<start+len; i++)
            {
                inl = AnnotText.Inlines.ElementAt(i);
                inl.TextDecorations = TextDecorations.Baseline;
            } */
            
        }

        private void moveMarkToAnnotation(XElement note)
        {
            int start = (int)note.Element("data").Element("startfragment")-1;
            int len = (int)note.Element("data").Element("numberofwords");
            moveCursor(start, len);
            fillForm(note);
        }

        private void Button_Click_GoToNextAnnotation(object sender, RoutedEventArgs e) //Next Annotation
        {
            if (updateInProgress)
            {
                saveUpdatedInfo(currentNote);
            }
            MoveToNextAnnotation();
        }

        private void MoveToNextAnnotation()
        {
            noteIndex++;
            if (!NoteBack.IsEnabled)
                NoteBack.IsEnabled = true;
            if (noteIndex == updatedNotes.Count - 1)
                NoteForward.IsEnabled = false;
            if (noteIndex > finishNoteOfCurrentTag)
            {
                initVariables(noteIndex);
                //showChapterElement(noteIndex, findEndNote(noteIndex));
                /*startNoteOfCurrentTag = noteIndex;
                chapterElementNr = getTagNumber(noteIndex);
                finishNoteOfCurrentTag = findEndNote(noteIndex);
                changeChapterElement();
                showText(); */
            }
            else
            {
                moveMarkToAnnotation(updatedNotes[noteIndex]);
            }
        }

        

        private void MoveToPreviousAnnotation()
        {
            noteIndex--;
            if (!NoteForward.IsEnabled)
                NoteForward.IsEnabled = true;
            if (noteIndex == 0)
                NoteBack.IsEnabled = false;
            if (noteIndex < startNoteOfCurrentTag)
            {
                inForwardDirection = false;
                initVariables(noteIndex);
                //showChapterElement(findInitialNote(noteIndex), noteIndex);
                /*startNoteOfCurrentTag = findInitialNote(noteIndex);
                finishNoteOfCurrentTag = noteIndex;
                changeChapterElement(true,false);
                showText(); */
            }

            else
            {
                moveMarkToAnnotation(updatedNotes[noteIndex]);
            }
        }

        private void MoveToNextChapterElement()
        {

        }

        private void Button_Click_GoToPreviousAnnotation(object sender, RoutedEventArgs e) //Previous Annotation
        {
            if (updateInProgress)
            {
                saveUpdatedInfo(currentNote);
            }
            MoveToPreviousAnnotation();
        }

        private void WordBack_Click(object sender, RoutedEventArgs e)
        {
            if (updateInProgress)
            {
                saveUpdatedInfo(currentNote);
            }
            inAnnotationMode = false;
            inForwardDirection = false;
            underlineWord(underlinedElement-1,1);
            if (underlinedElement == 0)
                //WordBack.IsEnabled = false;
                if (underlinedElement < textLength - 1) ;
                //WordForward.IsEnabled = true;

        }

        private void WordForward_Click(object sender, RoutedEventArgs e)
        {
            if (updateInProgress)
            {
                saveUpdatedInfo(currentNote);
            }
            inAnnotationMode = false;
            inForwardDirection = true;
            underlineWord(underlinedElement+1,1);
            if (underlinedElement == textLength - 1)
                //WordForward.IsEnabled = false;
                if (underlinedElement > 0) ;
                //WordBack.IsEnabled = true;
        }

        private void AnnotationType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AnnotationCategory.ItemsSource = AnnotCategoriesItems[AnnotationType.SelectedIndex];
        }

        private void StartToLeft_Click(object sender, RoutedEventArgs e)
        {
            if (!updateInProgress)
                startUpdate(currentNote);
            newStart--;
            newLength++;
            changeHighlight(tagsTextDividedIntoWords, newStart, "#FFFF00");
            
        }

        private void StartToRight_Click(object sender, RoutedEventArgs e)
        {
            if (!updateInProgress)
                startUpdate(currentNote);
            newStart++;
            newLength--;
            changeHighlight(tagsTextDividedIntoWords, newStart-1, neutralBackground);
        }

        private void EndToLeft_Click(object sender, RoutedEventArgs e)
        {
            int oldEnd;
            if (!updateInProgress)
                startUpdate(currentNote);
            oldEnd = newStart + newLength - 1;
            newLength--;

            changeHighlight(tagsTextDividedIntoWords, oldEnd, neutralBackground);
        }

        private void EndToRight_Click(object sender, RoutedEventArgs e)
        {
            int newEnd;
            if (!updateInProgress)
                startUpdate(currentNote);
            newLength++;
            newEnd = newStart + newLength - 1;
            changeHighlight(tagsTextDividedIntoWords, newEnd, "#FFFF00");
        }

        private void ChangedConfirmed_Click(object sender, RoutedEventArgs e)
        {
            if (AnnotationChangesConfirmed != null)
                AnnotationChangesConfirmed(sender, e);
            this.Hide();
            
        } 
    }
}
