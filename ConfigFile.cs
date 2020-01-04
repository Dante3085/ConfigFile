
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;

// TODO: Fehlendes Semikolon am Ende einer Sektion ist sehr schlecht. Komische Fehler, die nicht abgefangen werden.

namespace ConfigFile
{
    public enum EType
    {
        BOOL,
        INT,
        FLOAT,
        DOUBLE,
        STRING,

        LIST_BOOL,
        LIST_INT,
        LIST_FLOAT,
        LIST_DOUBLE,
        LIST_STRING,

        LIST_LIST_BOOL,
        LIST_LIST_INT,
        LIST_LIST_FLOAT,
        LIST_LIST_DOUBLE,
        LIST_LIST_STRING,
    }

    public class SectionAttribute
    {
        public String Name { get; set; }
        public Object Value { get; set; }
        public EType Type { get; set; }

        public SectionAttribute(String name, Object value, EType type)
        {
            Name = name;
            Value = value;
            Type = type;
        }

        public override string ToString()
        {
            // TODO: Benutze anstatt "Value.ToString()" hier später die Methode,
            //       die in Stringformat der Datei umwandelt für einheitlichkeit.
            return Type.ToString() + ": " + Name + " = " + Value.ToString() + ";";
        }
    }

    public class Section
    {
        public String Name { get; set; }
        public String Category { get; set; }
        public List<SectionAttribute> Attributes { get; set; }

        public Section(String name, String category)
        {
            Name = name;
            Category = category;
            Attributes = new List<SectionAttribute>();
        }

        public override string ToString()
        {
            if (Category != null)
            {
                return "[" + Category + ": " + Name + "]";
            }
            else
            {
                return "[" + Name + "]";
            }
        }
    }

    public class Category
    {
        public String Name { get; set; }
        public List<Section> Sections { get; set; }

        public Category(String name)
        {
            Name = name;
            Sections = new List<Section>();
        }

        public override string ToString()
        {
            return Name;
        }
    }

    public class ConfigFile
    {
        private List<Section> sections = new List<Section>();
        private List<Category> categories = new List<Category>();

        private static Dictionary<String, EType> stringToType = new Dictionary<string, EType>()
        {
            { "bool", EType.BOOL },
            { "int", EType.INT },
            { "float", EType.FLOAT },
            { "double", EType.DOUBLE },
            { "string", EType.STRING },

            { "List<bool>", EType.LIST_BOOL },
            { "List<int>", EType.LIST_INT },
            { "List<float>", EType.LIST_FLOAT },
            { "List<double>", EType.LIST_DOUBLE },
            { "List<string>", EType.LIST_STRING },

            { "List<List<bool>>", EType.LIST_LIST_BOOL },
            { "List<List<int>>", EType.LIST_LIST_INT },
            { "List<List<float>>", EType.LIST_LIST_FLOAT },
            { "List<List<double>>", EType.LIST_LIST_DOUBLE },
            { "List<List<string>>", EType.LIST_LIST_STRING },
        };

        public ConfigFile(String path)
        {
            ParseFileContents(File.ReadAllText(path));
        }

        private void ParseFileContents(String fileContents)
        {
            fileContents = Regex.Replace(fileContents, @"\s+", String.Empty);

            // TODO: Remove Comments

            int indexFirstClosingSquareBracket = fileContents.IndexOf(']');
            Section currentSection = ParseSectionHeader(fileContents.Substring(0, indexFirstClosingSquareBracket + 1));
            fileContents = fileContents.Remove(0, indexFirstClosingSquareBracket + 1);

            int previousFileContentsLength = fileContents.Length;

            while (fileContents.Length != 0)
            {
                int indexNextClosingSquareBracket = fileContents.IndexOf(']');
                int indexNextSemiColon = fileContents.IndexOf(';');
                int indexNextEquals = fileContents.IndexOf('=');

                // Found new Section.
                if ((indexNextClosingSquareBracket != -1) && indexNextClosingSquareBracket < indexNextSemiColon)
                {
                    // Check if the last SectionAttribute of the currentSection has a missing semicolon. 
                    if (indexNextEquals != -1 && indexNextEquals < indexNextClosingSquareBracket)
                    {
                        throw new ArgumentException("Last SectionAttribute of Section \"" + currentSection.Name + "\" has a missing semicolon(;).");
                    }

                    // Handle finished Section.
                    AddSection(currentSection);

                    // Create new Section.
                    currentSection = ParseSectionHeader(fileContents.Substring(0, indexNextClosingSquareBracket + 1));
                    fileContents = fileContents.Remove(0, indexNextClosingSquareBracket + 1);
                }

                // Found new SectionAttribute
                else if (indexNextSemiColon != -1)
                {
                    currentSection.Attributes.Add(ParseSectionAttribute(fileContents.Substring(0, indexNextSemiColon + 1), currentSection.Name));
                    fileContents = fileContents.Remove(0, indexNextSemiColon + 1);
                }

                if (previousFileContentsLength == fileContents.Length)
                {
                    throw new ArgumentException("Last SectionAttribute in Section \"" + currentSection.Name + "\" is missing a semicolon(;).");
                }
                previousFileContentsLength = fileContents.Length;
            }

            AddSection(currentSection);
        }

        private void AddSection(Section section)
        {
            sections.Add(section);
            if (section.Category != null)
            {
                Category category = categories.Find((c) => c.Name == section.Category);
                if (category != null)
                {
                    category.Sections.Add(section);
                }
                else
                {
                    category = new Category(section.Category);
                    category.Sections.Add(section);
                    categories.Add(category);
                }
            }
        }

        private Section ParseSectionHeader(String headerString)
        {
            // Remove '[' and ']' from headerString.
            headerString = headerString.Remove(0, 1);
            headerString = headerString.Remove(headerString.Length - 1, 1);

            int indexDoubleColon = headerString.IndexOf(':');

            // SectionHeader has Category and Name.
            if (indexDoubleColon != -1)
            {
                String[] categoryAndName = headerString.Split(':');
                return new Section(categoryAndName[1], categoryAndName[0]);
            }

            // SectionHeader has only Name.
            else
            {
                return new Section(headerString, null);
            }
        }

        private SectionAttribute ParseSectionAttribute(String attributeString, String sectionName)
        {
            int numDoubleColons = attributeString.Count(c => c == ':'); ;
            int numEquals = attributeString.Count(c => c == '=');
            int numSemicolons = attributeString.Count(c => c == ';');

            if (numDoubleColons != 1 || numEquals != 1 || numSemicolons != 1)
            {
                throw new ArgumentException("Syntax error in one or more SectionAttributes in Section \"" + sectionName + "\".");
            }

            EType type;
            String name;
            Object value;

            int indexDoubleColon = attributeString.IndexOf(':');
            int indexEquals = attributeString.IndexOf('=');
            int indexSemicolon = attributeString.IndexOf(';');

            String typeString = attributeString.Substring(0, indexDoubleColon);
            name = attributeString.Substring(indexDoubleColon + 1, indexEquals - (indexDoubleColon + 1)); 
            String valueString = attributeString.Substring(indexEquals + 1, indexSemicolon - (indexEquals + 1));

            if (!stringToType.TryGetValue(typeString, out type))
            {
                throw new ArgumentException("Given type \"" + typeString + "\" does not exist.");
            }

            value = StringToType(valueString, type);

            return new SectionAttribute(name, value, type);
        }

        private Object StringToType(String s, EType type)
        {
            Object returnValue;

            if (type == EType.LIST_BOOL   ||
                type == EType.LIST_INT    ||
                type == EType.LIST_FLOAT  ||
                type == EType.LIST_DOUBLE ||
                type == EType.LIST_STRING)
            {
                returnValue = ListStringToType(s, type);
            }
            else if (type == EType.LIST_LIST_BOOL   ||
                     type == EType.LIST_LIST_INT    ||
                     type == EType.LIST_LIST_FLOAT  ||
                     type == EType.LIST_LIST_DOUBLE ||
                     type == EType.LIST_LIST_STRING)
            {
                returnValue = List2dStringToType(s, type);
            }
            else
            {
                switch (type)
                {
                    case EType.BOOL:
                        returnValue = StringToBool(s);
                        break;

                    case EType.INT:
                        returnValue = StringToInt(s);
                        break;

                    case EType.FLOAT:
                        returnValue = StringToFloat(s);
                        break;

                    case EType.DOUBLE:
                        returnValue = StringToDouble(s);
                        break;

                    case EType.STRING:
                        returnValue = StringToString(s);
                        break;

                    default:
                        throw new ArgumentException("Given type \"" + type.ToString() + "\" is not supported.");
                }
            }

            return returnValue;
        }

        private Object ListStringToType(String s, EType type)
        {
            Object returnValue = null;

            s = s.Remove(0, 1);
            s = s.Remove(s.Length - 1, 1);

            String[] elements = s.Split(',');

            switch(type)
            {
                case EType.LIST_BOOL:
                    {
                        List<bool> list = new List<bool>();
                        foreach (String boolString in elements)
                        {
                            list.Add(StringToBool(boolString));
                        }
                        returnValue = list;

                        break;
                    }

                case EType.LIST_INT:
                    {
                        List<int> list = new List<int>();
                        foreach (String intString in elements)
                        {
                            list.Add(StringToInt(intString));
                        }
                        returnValue = list;

                        break;
                    }

                case EType.LIST_FLOAT:
                    {
                        List<float> list = new List<float>();
                        foreach (String floatString in elements)
                        {
                            list.Add(StringToFloat(floatString));
                        }
                        returnValue = list;

                        break;
                    }

                case EType.LIST_DOUBLE:
                    {
                        List<double> list = new List<double>();
                        foreach (String doubleString in elements)
                        {
                            list.Add(StringToDouble(doubleString));
                        }
                        returnValue = list;

                        break;
                    }

                case EType.LIST_STRING:
                    {
                        // TODO: Dämliche Bennenung StringToString und stringString.
                        List<string> list = new List<string>();
                        foreach (String stringString in elements)
                        {
                            list.Add(StringToString(stringString));
                        }
                        returnValue = list;

                        break;
                    }

                default:
                    throw new ArgumentException("Given type \"" + type.ToString() + "\" is not a supported list type.");
            }

            return returnValue;
        }

        private Object List2dStringToType(String s, EType type)
        {
            Object returnValue = null;

            s = s.Remove(0, 1);
            s = s.Remove(s.Length - 1, 1);

            String[] elements = s.Split(new String[] { "}," }, StringSplitOptions.None);
            
            for (int i = 0; i < elements.Length - 1; ++i)
            {
                elements[i] += "}";
            }

            switch(type)
            {
                case EType.LIST_LIST_BOOL:
                    {
                        List<List<bool>> listOfLists = new List<List<bool>>();
                        foreach(String listString in elements)
                        {
                            listOfLists.Add((List<bool>)ListStringToType(listString, EType.LIST_BOOL));
                        }
                        returnValue = listOfLists;

                        break;
                    }
                case EType.LIST_LIST_INT:
                    {
                        List<List<int>> listOfLists = new List<List<int>>();
                        foreach (String listString in elements)
                        {
                            listOfLists.Add((List<int>)ListStringToType(listString, EType.LIST_INT));
                        }
                        returnValue = listOfLists;

                        break;
                    }
                case EType.LIST_LIST_FLOAT:
                    {
                        List<List<float>> listOfLists = new List<List<float>>();
                        foreach (String listString in elements)
                        {
                            listOfLists.Add((List<float>)ListStringToType(listString, EType.LIST_FLOAT));
                        }
                        returnValue = listOfLists;

                        break;
                    }
                case EType.LIST_LIST_DOUBLE:
                    {
                        List<List<double>> listOfLists = new List<List<double>>();
                        foreach (String listString in elements)
                        {
                            listOfLists.Add((List<double>)ListStringToType(listString, EType.LIST_DOUBLE));
                        }
                        returnValue = listOfLists;

                        break;
                    }
                case EType.LIST_LIST_STRING:
                    {
                        List<List<string>> listOfLists = new List<List<string>>();
                        foreach (String listString in elements)
                        {
                            listOfLists.Add((List<string>)ListStringToType(listString, EType.LIST_STRING));
                        }
                        returnValue = listOfLists;

                        break;
                    }

                default:
                    throw new ArgumentException("Given type \"" + type.ToString() + "\" is not a supported list list type.");
            }

            return returnValue;
        }

        private bool StringToBool(String s)
        {
            return bool.Parse(s);
        }

        private int StringToInt(String s)
        {
            return int.Parse(s);
        }

        private float StringToFloat(String s)
        {
            return float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        }

        private double StringToDouble(String s)
        {
            return double.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        }

        private String StringToString(String s)
        {
            return s.Replace("\"", "");
        }
    }
}
