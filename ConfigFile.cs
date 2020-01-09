
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Globalization;
using System.Text;


// TODO: Beim hinzufügen von neuen SectionAttributes ist nicht gegeben das der gegebene EType und der gegebene Value
//       übereinstimmen.

// TODO: Falls in der Datei ein SectionAttribute einen Value mit falscher Syntax hat, sollte ein entsprechender Fehler
//       mit Ort und Art des Fehlers geschmissen werden.

// TODO: Veränderung von Categorys, Sections und SectionAttributes über die einfachen Get-Methoden ist schlecht.
//       Es sollten nur explizite Manipulationsmethoden wie AppendSectionAttribute() verwendet werden.

// TODO: Manipulationsmethoden für Category und Section.

// TODO: Diese Klasse ist viel zu lang und hat zu viel Redundanten Code. Debugging der StringToValue() und ValueToString()
// Abläufe ist auch schwer.

// LESSON: String Konkatenation in Schleifen ist schleeeeeeeecht. Falls Strings in der gegebenen Sprache immutable sind,
//         werden sie immer kopiert und neu erzeugt.

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
            return ConfigFile.SectionAttributeToString(this);
        }
    }

    public class Section
    {
        public String Name { get; set; }
        public String Category { get; set; }
        public List<SectionAttribute> Attributes 
        {
            get;
            set; 
        }

        public Section(String name, String category, List<SectionAttribute> attributes = null)
        {
            Name = name;
            Category = category;

            if (attributes == null)
            {
                Attributes = new List<SectionAttribute>();
            }
            else
            {
                Attributes = attributes;
            }
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
        private String path;

        private List<Section> sections = new List<Section>();
        private List<Category> categories = new List<Category>();

        private static int estimatedCharactersPerSectionString = 150;

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
            this.path = path;

            ParseFileContents(File.ReadAllText(path));
        }

        #region PublicInterface

        #region SectionAttribute

        public SectionAttribute GetSectionAttribute(String sectionName, String attributeName)
        {
            Section section = sections.Find(s => s.Name == sectionName);
            if (section == null)
            {
                throw new ArgumentException("Given Section \"" + sectionName + "\" does not exist.");
            }

            SectionAttribute sectionAttribute = section.Attributes.Find(a => a.Name == attributeName);
            if (sectionAttribute == null)
            {
                throw new ArgumentException("Given SectionAttribute \"" + sectionAttribute + "\" does not exist.");
            }

            return sectionAttribute;
        }

        public void ChangeSectionAttribute(String sectionName, String attributeName, SectionAttribute newAttribute, bool writeToFile = true)
        {
            Section section = sections.Find(s => s.Name == sectionName);
            if (section == null)
            {
                throw new ArgumentException("Given Section \"" + sectionName + "\" does not exist.");
            }

            SectionAttribute existingAttribute = section.Attributes.Find(a => a.Name == attributeName);
            if (existingAttribute == null)
            {
                throw new ArgumentException("Given SectionAttribute \"" + attributeName + "\" does not exist in Section \"" +
                                            sectionName + "\".");
            }

            // Renaming existingAttribute.
            if (existingAttribute.Name != newAttribute.Name)
            {
                // We can't rename to an already existing name.
                if (section.Attributes.Find(a => a.Name == newAttribute.Name) != null)
                {
                    throw new ArgumentException("Given SectionAttribute \"" + newAttribute.Name + "\" already exists in Section" +
                                                "\"" + sectionName + "\".");
                }
            }

            existingAttribute.Name = newAttribute.Name;
            existingAttribute.Type = newAttribute.Type;
            existingAttribute.Value = newAttribute.Value;

            if (writeToFile)
            {
                WriteSectionsToFile();
            }
        }

        public void RemoveSectionAttribute(String sectionName, String attributeName, bool writeToFile = true)
        {
            Section section = sections.Find(s => s.Name == sectionName);
            if (section == null)
            {
                throw new ArgumentException("Given Section \"" + sectionName + "\" does not exist.");
            }

            SectionAttribute existingAttribute = section.Attributes.Find(a => a.Name == attributeName);
            if (existingAttribute == null)
            {
                throw new ArgumentException("Given SectionAttribute \"" + attributeName + "\" does not exist in Section \"" +
                                            sectionName + "\".");
            }
            section.Attributes.Remove(existingAttribute);

            if (writeToFile)
            {
                WriteSectionsToFile();
            }
        }

        public void RemoveSectionAttributeAt(String sectionName, int index, bool writeToFile = true)
        {
            Section section = sections.Find(s => s.Name == sectionName);
            if (section == null)
            {
                throw new ArgumentException("Given Section \"" + sectionName + "\" does not exist.");
            }
            section.Attributes.RemoveAt(index);

            if (writeToFile)
            {
                WriteSectionsToFile();
            }
        }

        public void RemoveSectionAttributeRange(String sectionName, int index, int count, bool writeToFile = true)
        {
            Section section = sections.Find(s => s.Name == sectionName);
            if (section == null)
            {
                throw new ArgumentException("Given Section \"" + sectionName + "\" does not exist.");
            }

            section.Attributes.RemoveRange(index, count);

            if (writeToFile)
            {
                WriteSectionsToFile();
            }
        }

        public void AppendSectionAttribute(String sectionName, SectionAttribute newAttribute, bool writeToFile = true)
        {
            Section section = sections.Find(s => s.Name == sectionName);
            if (section == null)
            {
                throw new ArgumentException("Given Section \"" + sectionName + "\" does not exist.");
            }


            SectionAttribute existingAttribute = section.Attributes.Find(a => a.Name == newAttribute.Name);
            if (existingAttribute != null)
            {
                throw new ArgumentException("Given SectionAttribute \"" + newAttribute.Name + "\" already exists in given" +
                                            "Section \"" + sectionName + "\".");
            }

            section.Attributes.Add(newAttribute);

            if (writeToFile)
            {
                WriteSectionsToFile();
            }
        }

        public void AppendSectionAttributeRange(String sectionName, List<SectionAttribute> newAttributes, bool writeToFile = true)
        {
            Section section = sections.Find(s => s.Name == sectionName);
            if (section == null)
            {
                throw new ArgumentException("Given Section \"" + sectionName + "\" does not exist.");
            }

            // Do the newAttributes and the existing Attributes in the given Section have a SectionAttribute
            // with the same name (i.e. the same SectionAttribute).
            if (newAttributes.Select(a => a.Name).Intersect(section.Attributes.Select(a => a.Name)).Any())
            {
                throw new ArgumentException("Given SectionAttributes contain a SectionAttribute that is already in the given Section \"" + sectionName + "\".");
            }

            section.Attributes.AddRange(newAttributes);

            if (writeToFile)
            {
                WriteSectionsToFile();
            }
        }

        public void InsertSectionAttribute(String sectionName, int index, SectionAttribute newAttribute, bool writeToFile = true)
        {
            Section section = sections.Find(s => s.Name == sectionName);
            if (section == null)
            {
                throw new ArgumentException("Given Section \"" + sectionName + "\" does not exist.");
            }

            if (section.Attributes.Find(a => a.Name == newAttribute.Name) != null)
            {
                throw new ArgumentException("Given SectionAttribute \"" + newAttribute.Name + "\" already exists in Section" +
                                                "\"" + sectionName + "\".");
            }

            section.Attributes.Insert(index, newAttribute);

            if (writeToFile)
            {
                WriteSectionsToFile();
            }
        }

        public void InsertSectionAttributeRange(String sectionName, int index, List<SectionAttribute> newAttributes, bool writeToFile = true)
        {
            Section section = sections.Find(s => s.Name == sectionName);
            if (section == null)
            {
                throw new ArgumentException("Given Section \"" + sectionName + "\" does not exist.");
            }

            if (newAttributes.Select(a => a.Name).Intersect(section.Attributes.Select(a => a.Name)).Any())
            {
                throw new ArgumentException("Given SectionAttributes contain a SectionAttribute that is already in the given Section \"" + sectionName + "\".");
            }

            section.Attributes.InsertRange(index, newAttributes);

            if (writeToFile)
            {
                WriteSectionsToFile();
            }
        }

        #endregion

        #region Section

        public Section GetSection(String sectionName)
        {
            Section section = sections.Find(s => s.Name == sectionName);
            if (section == null)
            {
                throw new ArgumentException("Given Section \"" + sectionName + "\" does not exist.");
            }

            return section;
        }

        public void ChangeSection(String sectionName, Section newSection, bool writeToFile = true)
        {
            Section section = sections.Find(s => s.Name == sectionName);
            if (section == null)
            {
                throw new ArgumentException("Given Section \"" + sectionName + "\" does not exist.");
            }

            if (sectionName != newSection.Name)
            {
                if (sections.Find(s => s.Name == newSection.Name) != null)
                {
                    throw new ArgumentException("Given newSection \"" + newSection.Name + "\" already exists in this ConfigFile.");
                }
            }

            section.Category = newSection.Category;
            section.Name = newSection.Name;
            section.Attributes = newSection.Attributes;

            if (writeToFile)
            {
                WriteSectionsToFile();
            }
        }

        public void ChangeSectionHeader(String sectionName, String newCategoryName = null, String newSectionName = null, bool writeToFile = true)
        {
            Section section = sections.Find(s => s.Name == sectionName);
            if (section == null)
            {
                throw new ArgumentException("Given Section \"" + sectionName + "\" does not exist.");
            }

            if (newCategoryName != null)
            {
                section.Category = newCategoryName;
            }
            else if (newSectionName != null)
            {
                section.Name = newSectionName;
            }

            if (writeToFile)
            {
                WriteSectionsToFile();
            }
        }

        public void AppendSection(Section section, bool writeToFile = true)
        {
            if (sections.Find(s => s.Name == section.Name) != null)
            {
                throw new ArgumentException("Given Section \"" + section.Name + "\" already exists.");
            }

            sections.Add(section);

            if (writeToFile)
            {
                WriteSectionsToFile();
            }
        }

        public void AppendSectionRange(List<Section> sections, bool writeToFile = true)
        {
            throw new NotImplementedException();
        }

        #endregion

        public Category GetCategory(String categoryName)
        {
            Category category = categories.Find(c => c.Name == categoryName);
            if (category == null)
            {
                throw new ArgumentException("Given Category \"" + categoryName + "\" does not exist.");
            }

            return category;
        }

        public void WriteSectionsToFile()
        {
            // String fileContents = "\n";

            StringBuilder sb = new StringBuilder(estimatedCharactersPerSectionString * sections.Count);
            sb.Append("\n");

            foreach (Section section in sections)
            {
                sb.Append(SectionToString(section));
                sb.Append("\n");

                // fileContents += SectionToString(section) + "\n";
            }

            String fileContents = sb.ToString();
            fileContents = fileContents.Remove(fileContents.Length - 1, 1);

            // fileContents = fileContents.Remove(fileContents.Length - 1, 1);

            // TODO: Wenn hier kein Encoding eingetragen oder Encoding.Default genommen wird,
            // führt das manchmal dazu das in SublimeText3 Zeichen nicht angezeigt werden.
            // Eckige Klammer und ein paar normale Buchstaben. In anderen Texteditoren sind
            // diese Zeichen zunächst noch da. Bei wiederholtem WriteSectionsToFile() verschwinden
            // dann aber auch immer mehr Zeichen da. Ich weiß nicht genau warum dieses Problem besteht.
            File.WriteAllText(path, fileContents, System.Text.Encoding.UTF8);
        }

        #endregion

        #region PrivateImplementationDetails

        private String SectionToString(Section section)
        {
            String sectionString;
            if (section.Category == null)
            {
                sectionString = "[" + section.Name + "]\n";
            }
            else
            {
                sectionString = "[" + section.Category + ": " + section.Name + "]\n";
            }

            foreach (SectionAttribute sectionAttribute in section.Attributes)
            {
                sectionString += SectionAttributeToString(sectionAttribute) + "\n";
            }

            return sectionString;
        }

        public static String SectionAttributeToString(SectionAttribute sectionAttribute)
        {
            String typeString = stringToType.FirstOrDefault(s => s.Value == sectionAttribute.Type).Key;
            String valueString = ValueToString(sectionAttribute.Value, sectionAttribute.Type);

            return typeString + ": " + sectionAttribute.Name + " = " + valueString + ";";
        }

        private static String ValueToString(Object value, EType type)
        {
            if (value == null)
            {
                return "NULL";
            }

            String returnValue;

            if (type == EType.LIST_BOOL ||
                type == EType.LIST_INT ||
                type == EType.LIST_FLOAT ||
                type == EType.LIST_DOUBLE ||
                type == EType.LIST_STRING)
            {
                returnValue = ListToString(value, type);
            }
            else if (type == EType.LIST_LIST_BOOL ||
                     type == EType.LIST_LIST_INT ||
                     type == EType.LIST_LIST_FLOAT ||
                     type == EType.LIST_LIST_DOUBLE ||
                     type == EType.LIST_LIST_STRING)
            {
                returnValue = List2dToString(value, type);
            }
            else
            {
                switch (type)
                {
                    case EType.BOOL:
                        {
                            returnValue = BoolToString((bool)value);
                            break;
                        }
                    case EType.INT:
                        {
                            returnValue = IntToString((int)value);
                            break;
                        }
                    case EType.FLOAT:
                        {
                            returnValue = FloatToString((float)value);
                            break;
                        }
                    case EType.DOUBLE:
                        {
                            returnValue = DoubleToString((double)value);
                            break;
                        }
                    case EType.STRING:
                        {
                            returnValue = StringToFileString((string)value);
                            break;
                        }
                    default:
                        throw new ArgumentException("Given type \"" + type.ToString() + "\" is not supported.");
                }
            }
            return returnValue;
        }

        private static String ListToString(Object value, EType type)
        {
            String listString = "{";

            switch (type)
            {
                case EType.LIST_BOOL:
                    {
                        List<bool> list = (List<bool>)value;
                        foreach (bool b in list)
                        {
                            listString += BoolToString(b) + ", ";
                        }

                        break;
                    }
                case EType.LIST_INT:
                    {
                        List<int> list = (List<int>)value;
                        foreach (int i in list)
                        {
                            listString += IntToString(i) + ", ";
                        }

                        break;
                    }
                case EType.LIST_FLOAT:
                    {
                        List<float> list = (List<float>)value;
                        foreach (float f in list)
                        {
                            listString += FloatToString(f) + ", ";
                        }

                        break;
                    }
                case EType.LIST_DOUBLE:
                    {
                        List<double> list = (List<double>)value;
                        foreach (float f in list)
                        {
                            listString += FloatToString(f) + ", ";
                        }

                        break;
                    }
                case EType.LIST_STRING:
                    {
                        List<string> list = (List<string>)value;
                        foreach (string s in list)
                        {
                            listString += StringToFileString(s) + ", ";
                        }


                        break;
                    }

                default:
                    throw new ArgumentException("Given type \"" + type.ToString() + "\" is not a valid list type.");
            }

            if (listString.Contains(','))
            {
                listString = listString.Remove(listString.Length - 2);
            }
            listString += "}";
            return listString;
        }

        private static String List2dToString(Object value, EType type)
        {
            String listString = "{";

            switch (type)
            {
                case EType.LIST_LIST_BOOL:
                    {
                        List<List<bool>> list2d = (List<List<bool>>)value;
                        foreach (List<bool> bools in list2d)
                        {
                            listString += ListToString(bools, EType.LIST_BOOL) + ", ";
                        }

                        break;
                    }
                case EType.LIST_LIST_INT:
                    {
                        List<List<int>> list2d = (List<List<int>>)value;
                        foreach (List<int> ints in list2d)
                        {
                            listString += ListToString(ints, EType.LIST_INT) + ", ";
                        }

                        break;
                    }
                case EType.LIST_LIST_FLOAT:
                    {
                        List<List<float>> list2d = (List<List<float>>)value;
                        foreach (List<float> floats in list2d)
                        {
                            listString += ListToString(floats, EType.LIST_FLOAT) + ", ";
                        }

                        break;
                    }
                case EType.LIST_LIST_DOUBLE:
                    {
                        List<List<double>> list2d = (List<List<double>>)value;
                        foreach (List<double> doubles in list2d)
                        {
                            listString += ListToString(doubles, EType.LIST_DOUBLE) + ", ";
                        }

                        break;
                    }
                case EType.LIST_LIST_STRING:
                    {
                        List<List<string>> list2d = (List<List<string>>)value;
                        foreach (List<string> strings in list2d)
                        {
                            listString += ListToString(strings, EType.LIST_STRING) + ", ";
                        }

                        break;
                    }
            }

            if (listString.Contains(','))
            {
                listString = listString.Remove(listString.Length - 2);
            }
            listString += "}";
            return listString;
        }

        private static String BoolToString(bool b)
        {
            return b ? "true" : "false";
        }

        private static String IntToString(int i)
        {
            return i.ToString();
        }

        private static String FloatToString(float f)
        {
            return f.ToString(CultureInfo.InvariantCulture);
        }

        private static String DoubleToString(double d)
        {
            return d.ToString(CultureInfo.InvariantCulture);
        }

        private static String StringToFileString(string s)
        {
            return "\"" + s + "\"";
        }

        // TODO: fileContents.Remove() durch StringBuilder ersetzen.
        private void ParseFileContents(String fileContents)
        {
            fileContents = Regex.Replace(fileContents, @"\s+", String.Empty);

            if (fileContents == String.Empty)
                return;

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
                    currentSection.Attributes.Add(ParseSectionAttribute(fileContents.Substring(0, indexNextSemiColon + 1), currentSection));
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
                Category category = categories.Find(c => c.Name == section.Category);
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

        private SectionAttribute ParseSectionAttribute(String attributeString, Section currentSection)
        {
            int numDoubleColons = attributeString.Count(c => c == ':'); ;
            int numEquals = attributeString.Count(c => c == '=');
            int numSemicolons = attributeString.Count(c => c == ';');

            if (numDoubleColons != 1 || numEquals != 1 || numSemicolons != 1)
            {
                throw new ArgumentException("Syntax Error in SectionAttribute after \"" + currentSection.Attributes[currentSection.Attributes.Count - 1].Name +
                                            "\" in Section \"" + currentSection.Name + "\".");
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

            value = StringToValue(valueString, type);

            return new SectionAttribute(name, value, type);
        }

        private Object StringToValue(String s, EType type)
        {
            if (s == "NULL")
            {
                return null;
            }

            Object returnValue;

            if (type == EType.LIST_BOOL ||
                type == EType.LIST_INT ||
                type == EType.LIST_FLOAT ||
                type == EType.LIST_DOUBLE ||
                type == EType.LIST_STRING)
            {
                returnValue = ListStringToValue(s, type);
            }
            else if (type == EType.LIST_LIST_BOOL ||
                     type == EType.LIST_LIST_INT ||
                     type == EType.LIST_LIST_FLOAT ||
                     type == EType.LIST_LIST_DOUBLE ||
                     type == EType.LIST_LIST_STRING)
            {
                returnValue = List2dStringToValue(s, type);
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

        private Object ListStringToValue(String s, EType type)
        {
            Object returnValue = null;

            bool emptyList = false;
            if (s == "{}")
            {
                emptyList = true;
            }

            s = s.Remove(0, 1);
            s = s.Remove(s.Length - 1, 1);

            String[] elements = s.Split(',');

            switch (type)
            {
                case EType.LIST_BOOL:
                    {
                        if (emptyList)
                            return new List<bool>();

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
                        if (emptyList)
                            return new List<int>();

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
                        if (emptyList)
                            return new List<float>();

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
                        if (emptyList)
                            return new List<double>();

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
                        if (emptyList)
                            return new List<string>();

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

        private Object List2dStringToValue(String s, EType type)
        {
            Object returnValue = null;

            s = s.Remove(0, 1);
            s = s.Remove(s.Length - 1, 1);

            String[] elements = s.Split(new String[] { "}," }, StringSplitOptions.None);

            for (int i = 0; i < elements.Length - 1; ++i)
            {
                elements[i] += "}";
            }

            switch (type)
            {
                case EType.LIST_LIST_BOOL:
                    {
                        List<List<bool>> listOfLists = new List<List<bool>>();
                        foreach (String listString in elements)
                        {
                            listOfLists.Add((List<bool>)ListStringToValue(listString, EType.LIST_BOOL));
                        }
                        returnValue = listOfLists;

                        break;
                    }
                case EType.LIST_LIST_INT:
                    {
                        List<List<int>> listOfLists = new List<List<int>>();
                        foreach (String listString in elements)
                        {
                            listOfLists.Add((List<int>)ListStringToValue(listString, EType.LIST_INT));
                        }
                        returnValue = listOfLists;

                        break;
                    }
                case EType.LIST_LIST_FLOAT:
                    {
                        List<List<float>> listOfLists = new List<List<float>>();
                        foreach (String listString in elements)
                        {
                            listOfLists.Add((List<float>)ListStringToValue(listString, EType.LIST_FLOAT));
                        }
                        returnValue = listOfLists;

                        break;
                    }
                case EType.LIST_LIST_DOUBLE:
                    {
                        List<List<double>> listOfLists = new List<List<double>>();
                        foreach (String listString in elements)
                        {
                            listOfLists.Add((List<double>)ListStringToValue(listString, EType.LIST_DOUBLE));
                        }
                        returnValue = listOfLists;

                        break;
                    }
                case EType.LIST_LIST_STRING:
                    {
                        List<List<string>> listOfLists = new List<List<string>>();
                        foreach (String listString in elements)
                        {
                            listOfLists.Add((List<string>)ListStringToValue(listString, EType.LIST_STRING));
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

        #endregion
    }
}
