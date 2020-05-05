using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace EpubReaderWithAnnotations
{
    class TextSearch
    {

       static string mQuery = @"[\wÄÖÜÕäöüõ]*[aeiouäöüõ]{1}?(?=[\s\p{P}])";
       static string dQuery = @"[\wÄÖÜÕäöüõ]*(sse|le|ni|na|ta|ga){1}?(?=[\s\p{P}])";
       static string[] exc = new string[] {"kui","juba", "natuke","ja","siis"};
       static XDocument searchResult;

        private static XElement getDescriptionData()
        {
            XAttribute bAuthor = new XAttribute("author","");
            XAttribute bTitle = new XAttribute("title","");
            XAttribute bLanguage = new XAttribute("language","");
            XElement root = new XElement("annotations", bAuthor, bTitle, bLanguage);
            return root;
        }

        private static XElement getSingleSearchResult(string[] pText, bool certainResult, string fileName, string pNr, int wordNumber, int searchLength)
        {
            StringBuilder sbText = new StringBuilder();
            string categoryText;
            for (int i = wordNumber; i < wordNumber + searchLength; i++)
                sbText.Append(pText[i]);
            XElement addedTime = new XElement("addedtime",System.DateTime.Now.ToString());
            XElement updatedTime = new XElement("updatedtime");
            XElement headword = new XElement("headword");
            XElement text = new XElement("text", sbText.ToString());
            XElement type = new XElement("type", "search");
            if (certainResult)
                categoryText = "Certain";
            else
                categoryText = "Doubt";
            XElement category = new XElement("category", categoryText);
            XElement comment = new XElement("comment");
            XElement data = new XElement("data");
            XElement fName = new XElement("filename", fileName);
            XElement tagElement = new XElement("tagelement", pNr);
            XElement startFragment = new XElement("startfragment", wordNumber.ToString());
            XElement numberOfWords = new XElement("numberofwords", searchLength.ToString());
            data.Add(fName);
            data.Add(tagElement);
            data.Add(startFragment);
            data.Add(numberOfWords);
            XElement resultElement = new XElement("annotation");
            resultElement.Add(addedTime);
            resultElement.Add(updatedTime);
            resultElement.Add(headword);
            resultElement.Add(text);
            resultElement.Add(type);
            resultElement.Add(category);
            resultElement.Add(comment);
            resultElement.Add(data);

            return resultElement;
        }

        private static string[] textIntoWords(string s)
        {
            string regExExpression = @"([\p{L}’]+\s*)|([\p{P}]+\s*)";
            int ind = 0;
            int arraysymlen = 0;
            MatchCollection mc = Regex.Matches(s, regExExpression);
            string[] result = new string[mc.Count];
            foreach (Match match in mc)
            {
                result[ind] = match.Value;
                //arraysymlen = arraysymlen+result[ind].Length;
                ind++;
            }
            /*Console.WriteLine("string length is: "+ s.Length);
            Console.WriteLine("array sym length is: " + arraysymlen); */
            return result;
        }

        //static lookThroughTheBook(string[] names, )

        public static void PassData(List<string>[] pages, List<XElement>[] annotes)
        {
            Console.ReadLine();
        }

        private static void processRegexData (string text, string pNumber, string fileName, MatchCollection right, MatchCollection doubts, string[] exclusions)
        {
            string[] chapterWords = textIntoWords(text);
            int doubtCounter = 0;
            int mStartIndex = -1;
            int mFinishIndex = -1;
            int textWordIndex = 0;
            int textCurrentPosition = 0;
            int textNextPosition = 0;
            int matchWordNumber = 0;
            int matchLength = 0;
            int i;
            int doubtsLen = doubts.Count;
            bool certain;
            foreach (Match match in right)
            {
                if (Array.IndexOf(exclusions, match.Value.ToLower()) >= 0)
                {
                    //Console.WriteLine("Exclusion - value: " + match.Value + " at:" + match.Index);
                    continue;
                }
                mStartIndex = match.Index;
                mFinishIndex = match.Index + match.Length - 1;
                for (i = textWordIndex; i < chapterWords.Length; i++)
                {
                    textNextPosition = textCurrentPosition + chapterWords[i].Length;
                    if (mStartIndex > textCurrentPosition)
                        textCurrentPosition = textNextPosition;
                    else
                    {
                        if (matchLength == 0)
                            matchWordNumber = i;
                        matchLength++;
                        if (textNextPosition > mFinishIndex)
                        {

                            textWordIndex = i + 1;
                            textCurrentPosition = textNextPosition;
                            break;

                        }
                    }

                }

                if (doubtCounter < doubtsLen && match.Value == doubts[doubtCounter].Value)
                {
                    //Console.WriteLine("Doubt: " + "word nr " + matchWordNumber + "- " + chapterWords[matchWordNumber] + " length: " + matchLength);
                    certain = false;
                    doubtCounter++;
                }
                else
                {
                    certain = true;
                    //Console.WriteLine("Right: word nr " + matchWordNumber + "- " + chapterWords[matchWordNumber] + " length: " + matchLength);
                    
                }
                searchResult.Root.Add(getSingleSearchResult(chapterWords, certain, fileName, pNumber, matchWordNumber, matchLength));
                matchWordNumber = matchLength = 0;
            }
            searchResult.Save("testsearch.annot");
        }


        public static void searchText(List<string>[] book, string[] bookFiles/*, string mainQuery, string doubtQuery, string[] exclusions*/)
        {
            string mainQuery = mQuery;
            string doubtQuery = dQuery;
            string[] exclusions = exc;
            string[] chapterWords;
            MatchCollection right, doubts;
            searchResult = new XDocument(getDescriptionData());
            int pCount;
            
            for(int i =0; i<book.Length; i++)
            {
                if (book[i].Count > 0) {
                    pCount = 0;
                    foreach (string p in book[i])
                        if (p != null)
                        {
                            chapterWords = textIntoWords(p);
                            right = Regex.Matches(p, mainQuery);
                            doubts = Regex.Matches(p, doubtQuery);
                            processRegexData(p, pCount.ToString(), bookFiles[i], right, doubts, exclusions);
                        }
                    pCount++;
                    Console.WriteLine(i + ". " + pCount);
                }
            }
        }
    }
}
