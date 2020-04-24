using System;
using System.Windows.Controls;
using mshtml;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EpubReaderWithAnnotations
{
    class BrowserOperations
    {
        WebBrowser screen;
        List<IHTMLElement> initialColl;
        List<XElement> i;

        BrowserOperations(WebBrowser wb)
        {
            this.screen = wb;
        }

        public void setScreen (WebBrowser wb)
        {
            
             this.screen = wb; 
        }

        

        private void highlightRange(IHTMLTxtRange range, string color)
        {
            range.execCommand("BackColor", false, color);
        }

        private IHTMLTxtRange getRangeFromAnnotation(BookData book, XElement el, IHTMLTxtRange range = null)
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
            return range;
        }


        private IHTMLTxtRange compareRanges(BookData book, int elementNr, IHTMLTxtRange range)
        { //Get annotations from nodes
                
            IEnumerable<XElement> setAnnotes = book.getAnnotesFromOneTag(elementNr);
            
            IHTMLTxtRange rangeCheck = getRange();
            //int annotNr;
            
            foreach (XElement el in setAnnotes)
            {
                rangeCheck = getRangeFromAnnotation(book, el, rangeCheck);
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
            range.execCommand("BackColor", false, color);
            return range.text;
        }

        private IHTMLTxtRange getRange()
        {
            IHTMLDocument2 document = screen.Document as IHTMLDocument2;
            IHTMLSelectionObject currentSelection = document.selection;
            IHTMLTxtRange range = currentSelection.createRange() as IHTMLTxtRange;
            return range;

        }

    }

    
}
