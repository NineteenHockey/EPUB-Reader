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
using System.Xml.Linq;

namespace EpubReaderWithAnnotations
{
    /// <summary>
    /// Interaction logic for SearchWindow.xaml
    /// </summary>
    public partial class SearchWindow : Window
    {
        public event EventHandler SearchConfirmed;
        static XElement searchCategories;
        static IEnumerable<XElement> categoriesByLanguage;
        string language;
        string[] names;
        XElement[] subCategories;
        bool isNoun;

        public string TextQuery
        {
            get { return textQuery.Text; }
        }

        public string CertainQuery
        {
            get { return subCategories[GrammarCategory.SelectedIndex].Element("certain").Value; }
        }

        public string DoubtQuery
        {
            get { return subCategories[GrammarCategory.SelectedIndex].Element("doubt").Value; }
        }

        public bool IsNoun
        {
            get
            {
                XElement element = subCategories[GrammarCategory.SelectedIndex].Parent;
                if (element.Attribute("speech").Value == "noun")
                    return true;
                else
                    return false;
            }
        }
    

        public SearchWindow(string lng)
        {
            InitializeComponent();
            language = lng;
            categoriesByLanguage = XElement.Load("GrammarCategories.xml").Elements("language");
            searchCategories = getLanguageGrammarCategories(language);
            GrammarType.ItemsSource = readCategoryList();
            this.Hide();
        }

        private XElement getLanguageGrammarCategories(string l)
        {
            foreach (XElement el in categoriesByLanguage)
            {
                if (el.Attribute("name").Value == l)
                    return el;
            }
            return null;
        }

        private static string[] readCategoryList()
        {
            var categories = from cat in searchCategories.Elements("category")
                             select cat;
            int len = categories.Count();
            XElement single;
            string[] result = new string[len+1];
            result[0] = "Choose category:";
            for (int i = 1; i < len+1; i++)
            {
                single = categories.ElementAt(i-1);
                result[i] = single.Attribute("name").Value;
            }
            return result;
        }

        /*public static string[][] getCategoryArray()
        {
            return mainCategories;
        }*/

        /*public static string[][] getSubCategoryArray()
        {
            return subCategories;
        } */

        private static XElement[] getSubCategories(string category, out string[] names)
        {
            names = null;
            var subCategories = from cat in searchCategories.Descendants("subcategory1")
                                where cat.Parent.Attribute("name").Value == category
                                select cat;
            if (subCategories == null)
                return null;
            int len = subCategories.Count();
            names = new string[len];
            XElement[] result = new XElement[len];
            //names = new string[len];
            for (int i = 0; i < len; i++)
            {
                result[i] = subCategories.ElementAt(i);
                names[i] = result[i].Attribute("name").Value;
                
            }
            return result;

        }

        /*private static string[][] createSubCategriesArray()
        {
            string[][] result = new string[mainCategories[0].Length][];
            string[] currentSubCategories;
            for (int i = 0; i < mainCategories[0].Length; i++)
            {
                currentSubCategories = getSubCategories(mainCategories[0][i]);
                result[i] = currentSubCategories;
            }
            return result;

        } */

        private void Button_Click(object sender, RoutedEventArgs e) //ok
        {

            if (SearchConfirmed != null)
                SearchConfirmed(sender, e);
            this.Hide();
        }

        private void GrammarType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            subCategories = getSubCategories(GrammarType.SelectedItem.ToString(), out names);
            GrammarCategory.ItemsSource = names;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e) //Cancel
        {
            this.Hide();
        }
    }
}
