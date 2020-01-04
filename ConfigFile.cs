
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

            switch(type)
            {
                case EType.BOOL:
                    returnValue = bool.Parse(s);
                    break;

                case EType.INT:
                    returnValue = int.Parse(s);
                    break;

                case EType.FLOAT:
                    returnValue = float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
                    break;

                case EType.DOUBLE:
                    returnValue = double.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
                    break;

                case EType.STRING:
                    returnValue = s.Replace("\"", "");
                    break;

                default:
                    throw new ArgumentException("Given type \"" + type.ToString() + "\" is not supported.");
            }

            return returnValue;
        }
    }
}
