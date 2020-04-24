using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.Threading.Tasks;
using mshtml;
using System.Windows.Controls;

namespace EpubReaderWithAnnotations
{
    public class BookData
    {
        private string _tempPath; // library dir
        private string _baseMenuXmlDiretory;
        private List<string> _menuItems; //book chapters?
        private int _currentPage;
        private  XDocument bookAnnotes;
        private IEnumerable<XElement> chapterAnnotes;
        private List<IHTMLElement> initialColl;
        private int _listBoxPrevInd = -1;
        private HTMLDocumentEvents2_Event _docEvent;
        private IHTMLDocument2 docOrg;
        
        bool isAssigned = false;

        public IEnumerable <XElement>getAnnotesFromOneTag (int elementNr)
        {
            IEnumerable<XElement> setAnnotes;
            XElement root = bookAnnotes.Root;
            
            //int annotNr;
            string bookFile = getFileName(_currentPage);
            setAnnotes =
            from el in root.Elements("annotation")
            where (((string)el.Element("data").Element("filename") == bookFile) && ((int)el.Element("data").Element("tagelement") == elementNr))
            select el;
            return setAnnotes;
        }

        

        BookData(string fileName)
        {
            _tempPath = Path.Combine("Library", fileName);
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
        }

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
    }

}
