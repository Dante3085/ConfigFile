
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Globalization;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;

/* OVERHAUL für benutzerdefinierte Typen
 1. Struktur des ConfigFiles analysieren, um die Aufgaben des Parsings einer
    Configdatei besser zu unterteilen.
 */

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

// TODO: Leerzeichen in Stringliteralen sind nach Parsing verschwunden.

// TODO: Erstellung von zusammengesetzten Typen im ConfigFile möglich machen.


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

        SECTION,
        USER_DEFINED,
    }

    public class SectionAttribute
    {
        public String Name { get; set; }
        public object Value { get; set; }
        public EType Type { get; set; }

        public Object this[String fieldName]
        {
            get
            {
                if (Type == EType.USER_DEFINED)
                    return Value.GetType().GetField(fieldName).GetValue(Value);
                return Value;
            }
        }

        public SectionAttribute(String name, object value, EType type)
        {
            Name = name;
            Value = value;
            Type = type;
        }

        public SectionAttribute(SectionAttribute sectionAttribute)
        {
            Name = sectionAttribute.Name;
            Value = sectionAttribute.Value;
            Type = sectionAttribute.Type;
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
        public SectionAttribute this[String attributeName]
        {
            get { return Attributes.Find((attribute) => attribute.Name == attributeName); }
        }

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

    public class UserDefinedType
    {
        // 
        public Type type;

        // attribute types und ob jedes user defined ist
        public List<Tuple<String, bool>> subtypes = new List<Tuple<string, bool>>();
    }

    public class ConfigFile
    {
        public Section this[String sectionName]
        {
            get { return sections.Find((s) => s.Name == sectionName); }
        }

        private static AssemblyName assemblyName = new AssemblyName("objectTypes");
        private static AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName,
            AssemblyBuilderAccess.Run);
        private static ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);

        private String path;
        private String fileContents;

        private List<Section> sections = new List<Section>();
        private List<Category> categories = new List<Category>();

        private static int estimatedCharactersPerSectionString = 150;

        private static Dictionary<String, EType> stringToEType = new Dictionary<string, EType>()
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

            { "Section", EType.SECTION },
        };
        private static Dictionary<String, Type> stringToType = new Dictionary<string, Type>()
        {
            { "bool",   typeof(bool) },
            { "int",    typeof(int) },
            { "float",  typeof(float) },
            { "double", typeof(double) },
            { "string", typeof(string) },

            { "List<bool>",   typeof(List<bool>) },
            { "List<int>",    typeof(List<int>) },
            { "List<float>",  typeof(List<float>) },
            { "List<double>", typeof(List<double>) },
            { "List<string>", typeof(List<string>) },

            { "List<List<bool>>",   typeof(List<List<bool>>) },
            { "List<List<int>>",    typeof(List<List<int>>) },
            { "List<List<float>>",  typeof(List<List<float>>) },
            { "List<List<double>>", typeof(List<List<double>>) },
            { "List<List<string>>", typeof(List<List<string>>) },
        };

        private Dictionary<String, UserDefinedType> userDefinedTypes = new Dictionary<string, UserDefinedType>();


        public String Path => path;

        public ConfigFile(String path)
        {
            this.path = path;
            this.fileContents = File.ReadAllText(path);

            ParseFileContents(fileContents);
        }

        #region PublicInterface

        public override String ToString()
        {
            return fileContents;
        }

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

        public void ChangeSectionAttribute(String sectionName, String attributeName, SectionAttribute newAttribute, bool writeToFile = false)
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

        public void AppendSectionAttribute(String sectionName, SectionAttribute newAttribute, bool writeToFile = false)
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

        public void AppendSectionAttributeRange(String sectionName, List<SectionAttribute> newAttributes, bool writeToFile = false)
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

        public void InsertSectionAttribute(String sectionName, int index, SectionAttribute newAttribute, bool writeToFile = false)
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

        public void InsertSectionAttributeRange(String sectionName, int index, List<SectionAttribute> newAttributes, bool writeToFile = false)
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

        public void RemoveSectionAttributeByName(String sectionName, String attributeName, bool writeToFile = false)
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

        public void RemoveSectionAttributeAt(String sectionName, int index, bool writeToFile = false)
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

        public void RemoveSectionAttributeRangeByNames(String sectionName, List<String> attributeNames, bool writeToFile = false)
        {
            Section section = sections.Find(s => s.Name == sectionName);
            if (section == null)
            {
                throw new ArgumentException("Given Section \"" + sectionName + "\" does not exist.");
            }

            List<String> difference = attributeNames.Except(section.Attributes.Select(a => a.Name)).ToList();
            if (difference.Count != 0)
            {
                throw new ArgumentException("In the given AttributeNames there are SectionAttributes that don't exist in the given Section " +
                                            "\"" + sectionName + "\". Those are " + ToString(difference));
            }

            section.Attributes.RemoveAll(a => attributeNames.Contains(a.Name));

            if (writeToFile)
            {
                WriteSectionsToFile();
            }
        }

        public void RemoveSectionAttributeRangeAt(String sectionName, int index, int count, bool writeToFile = false)
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

        public void ChangeSection(String sectionName, Section newSection, bool writeToFile = false)
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

        public void ChangeSectionHeader(String sectionName, String newCategoryName = null, String newSectionName = null, bool writeToFile = false)
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

        /// <summary>
        /// Tip: Don't use this Method if you want to append a lot of Sections in a loop at once.
        /// Rather use AppendSectionRange(). It's much faster.
        /// </summary>
        /// <param name="section"></param>
        /// <param name="writeToFile"></param>
        public void AppendSection(Section section, bool writeToFile = false)
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

        public void AppendSectionRange(List<Section> sections, bool writeToFile = false)
        {
            // TODO: Diese Fehlerbehandlung mit Angabe der Sektionen auch bei den anderen Methoden machen.
            List<String> namesCommonSections = this.sections.Select(s => s.Name).Intersect(sections.Select(s => s.Name)).ToList();
            if (namesCommonSections.Count != 0)
            {
                throw new ArgumentException("There is one or more Sections in the given Sections that already exists in this ConfigFile. " +
                                            "Those are " + ToString(namesCommonSections));
            }

            this.sections.AddRange(sections);

            if (writeToFile)
            {
                WriteSectionsToFile();
            }
        }

        public void InsertSection(int index, Section section, bool writeToFile = false)
        {
            if (sections.Find(s => s.Name == section.Name) != null)
            {
                throw new ArgumentException("Given Section \"" + section.Name + "\" already exists.");
            }

            sections.Insert(index, section);

            if (writeToFile)
            {
                WriteSectionsToFile();
            }
        }

        public void InsertSectionRange(int index, List<Section> sections, bool writeToFile = false)
        {
            List<String> namesCommonSections = this.sections.Select(s => s.Name).Intersect(sections.Select(s => s.Name)).ToList();
            if (namesCommonSections.Count != 0)
            {
                throw new ArgumentException("There is one or more Sections in the given Sections that already exists in this ConfigFile. " +
                                            "Those are " + ToString(namesCommonSections));
            }

            this.sections.InsertRange(index, sections);

            if (writeToFile)
            {
                WriteSectionsToFile();
            }
        }

        public void RemoveSectionByName(String sectionName, bool writeToFile = false)
        {
            if (sections.RemoveAll(s => s.Name == sectionName) == 0)
            {
                throw new ArgumentException("The given Section \"" + sectionName + "\" does not exist.");
            }

            if (writeToFile)
            {
                WriteSectionsToFile();
            }
        }

        public void RemoveSectionAt(int index, bool writeToFile = false)
        {
            sections.RemoveAt(index);

            if (writeToFile)
            {
                WriteSectionsToFile();
            }
        }

        public void RemoveSectionRangeByNames(List<String> sectionNames, bool writeToFile = false)
        {
            List<String> difference = sectionNames.Except(sections.Select(s => s.Name)).ToList();
            if (difference.Count != 0)
            {
                throw new ArgumentException("In the given SectionNames there are Sections that don't exist. Those are " + ToString(difference));
            }

            sections.RemoveAll(s => sectionNames.Contains(s.Name));

            if (writeToFile)
            {
                WriteSectionsToFile();
            }
        }

        public void RemoveSectionRangeAt(int index, int count, bool writeToFile)
        {
            sections.RemoveRange(index, count);

            if (writeToFile)
            {
                WriteSectionsToFile();
            }
        }

        #endregion

        #region Category

        public Category GetCategory(String categoryName)
        {
            Category category = categories.Find(c => c.Name == categoryName);
            if (category == null)
            {
                throw new ArgumentException("Given Category \"" + categoryName + "\" does not exist.");
            }

            return category;
        }

        public void ChangeCategoryName(String categoryName, String newCategoryName, bool writeToFile = false)
        {
            Category category = categories.Find(c => c.Name == categoryName);
            if (category == null)
            {
                throw new ArgumentException("Given Category \"" + categoryName + "\" does not exist.");
            }

            category.Name = newCategoryName;
            category.Sections.ForEach(s => s.Category = newCategoryName);

            if (writeToFile)
            {
                WriteSectionsToFile();
            }
        }

        public void RemoveCategory(String categoryName, bool writeToFile = false)
        {
            Category category = categories.Find(c => c.Name == categoryName);
            if (category == null)
            {
                throw new ArgumentException("Given Category \"" + categoryName + "\" does not exist.");
            }

            sections.RemoveAll(s => category.Sections.Contains(s));
            categories.Remove(category);

            if (writeToFile)
            {
                WriteSectionsToFile();
            }
        }

        #endregion

        public String ToString<T>(List<T> list)
        {
            StringBuilder builder = new StringBuilder("{ ", list.Count /* times someEstimatedStringLength */);

            foreach (T t in list)
            {
                builder.Append(t.ToString());
                builder.Append(", ");
            }

            builder.Remove(builder.Length - 2, 1);
            builder.Append("}");

            return builder.ToString();
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
                // TODO: Lieber StringBuilder. Das wird sonst sehr langsam, wenn eine Section
                // viele Attribute hat.
                sectionString += SectionAttributeToString(sectionAttribute) + "\n";
            }

            return sectionString;
        }

        public static String SectionAttributeToString(SectionAttribute sectionAttribute)
        {
            String typeString = stringToEType.FirstOrDefault(s => s.Value == sectionAttribute.Type).Key;
            String valueString = ValueToString(sectionAttribute.Value, sectionAttribute.Type);

            return typeString + ": " + sectionAttribute.Name + " = " + valueString + ";";
        }

        private static String ValueToString(object value, EType type)
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
                    case EType.SECTION:
                        {
                            returnValue = ((Section)value).Name;
                            break;
                        }
                    default:
                        throw new ArgumentException("Given type \"" + type.ToString() + "\" is not supported.");
                }
            }
            return returnValue;
        }

        private static String ListToString(object value, EType type)
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

        private static String List2dToString(object value, EType type)
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

        //private void ParseFileContents_OLD(String fileContents)
        //{
        //    // fileContents = Regex.Replace(fileContents, @"\s+", String.Empty);
        //    fileContents = fileContents.Replace("\n", String.Empty);
        //    fileContents = fileContents.Replace("\r", String.Empty);
        //    fileContents = fileContents.Replace(" ", String.Empty); // Entfernt auch Leerzeichen in Stringliteralen.

        //    if (fileContents == String.Empty)
        //        return;

        //    int currentStartIndex = 0;

        //    int indexNextOpenSquareBracket = fileContents.IndexOf('[', currentStartIndex);
        //    int indexNextClosedSquareBracket = fileContents.IndexOf(']', currentStartIndex);
        //    int indexNextSemicolon = -1;


        //    // Create Initial Section
        //    String headerString = fileContents.Substring(indexNextOpenSquareBracket, indexNextClosedSquareBracket - (indexNextOpenSquareBracket - 1));
        //    Section currentSection = ParseSectionHeader(headerString);

        //    currentStartIndex = indexNextClosedSquareBracket + 1;

        //    while (currentStartIndex != fileContents.Length)
        //    {
        //        indexNextOpenSquareBracket = fileContents.IndexOf('[', currentStartIndex);
        //        indexNextSemicolon = fileContents.IndexOf(';', currentStartIndex);

        //        // Next Section
        //        if (indexNextOpenSquareBracket != -1 &&
        //            (indexNextOpenSquareBracket - currentStartIndex) < (indexNextSemicolon - currentStartIndex))
        //        {
        //            // Add finished Section
        //            AddSection(currentSection);

        //            // Create new Section
        //            indexNextClosedSquareBracket = fileContents.IndexOf(']', currentStartIndex);
        //            currentSection = ParseSectionHeader(fileContents.Substring(indexNextOpenSquareBracket,
        //                                                                       indexNextClosedSquareBracket - (indexNextOpenSquareBracket - 1)));

        //            currentStartIndex = indexNextClosedSquareBracket + 1;
        //        }

        //        // Next SectionAttribute
        //        else
        //        {
        //            SectionAttribute currentSectionAttribute = ParseSectionAttribute(fileContents.Substring(currentStartIndex,
        //                                                              indexNextSemicolon - (currentStartIndex - 1)), currentSection);

        //            currentSection.Attributes.Add(currentSectionAttribute);

        //            currentStartIndex = indexNextSemicolon + 1;
        //        }
        //    }

        //    // Add last Section
        //    AddSection(currentSection);
        //}

        private void ParseFileContents(String fileContents)
        {
            if (fileContents == String.Empty)
                return;

            // Entferne redundante Zeichen.
            fileContents = fileContents.Replace("\n", String.Empty);
            fileContents = fileContents.Replace("\r", String.Empty);
            fileContents = fileContents.Replace(" ", String.Empty); // Entfernt auch Leerzeichen in Stringliteralen.

            String[] commands = fileContents.Split("%");

            foreach (String command in commands)
            {
                if (command.StartsWith("typedef")) 
                {
                    ProcessTypedef(command);
                }
                else if (command.StartsWith("section"))
                {
                    ProcessSection(command);
                }
                else if (command.StartsWith("include"))
                {
                    ProcessInclude(command);
                }
            }
        }

        private void ProcessTypedef(String typedefString)
        {
            // Neuen Typ in StringToValue behandeln.

            // Namen des neuen Typs herausfinden.
            // Checken, ob ein Typ mit demselben Namen bereits existiert.
            String typeName = GetStringInsideBrackets(typedefString.Substring(0, typedefString.IndexOf(']') + 1));
            if (userDefinedTypes.ContainsKey(typeName))
                throw new ArgumentException("The type '" + typeName + "' already exists.");

            // Neuen Typ definieren.
            TypeBuilder typeBuilder = moduleBuilder.DefineType(typeName);

            // Felder des Typs definieren.
            List<String> typeFields = typedefString.Remove(0, typedefString.IndexOf(']') + 1).Split(';').ToList();
            typeFields.RemoveAt(typeFields.Count - 1);

            UserDefinedType newUdt = new UserDefinedType();
            List<Type> constructorParameterTypes = new List<Type>();
            List<FieldBuilder> fieldBuilder = new List<FieldBuilder>();
            typeFields.ForEach((field) =>
            {
                String[] typeAndName = field.Split(':');
                fieldBuilder.Add(typeBuilder.DefineField(typeAndName[1], stringToType[typeAndName[0]], FieldAttributes.Public));
                constructorParameterTypes.Add(stringToType[typeAndName[0]]);

                newUdt.subtypes.Add(Tuple.Create<string, bool>(typeAndName[0],
                                    !stringToEType.ContainsKey(typeAndName[0])));
            });

            ConstructorBuilder constructorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard,
                constructorParameterTypes.ToArray());

            ILGenerator constructorIL = constructorBuilder.GetILGenerator();
            constructorIL.Emit(OpCodes.Ldarg_0);
            constructorIL.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));
            for (int i = 0; i < fieldBuilder.Count; ++i)
            {
                constructorIL.Emit(OpCodes.Ldarg_0);
                constructorIL.Emit(OpCodes.Ldarg_S, i + 1);
                constructorIL.Emit(OpCodes.Stfld, fieldBuilder[i]);
            }
            constructorIL.Emit(OpCodes.Ret);

            Type newType = typeBuilder.CreateType();
            newUdt.type = typeBuilder.CreateType();

            userDefinedTypes.Add(typeName, newUdt);

            // Neuen Typ in stringToType Dictionary ablegen.
            stringToType.Add(typeName, newType);
        }

        private void ProcessTypeField(TypeBuilder typeBuilder, String fieldString)
        {
            fieldString = fieldString.Remove(fieldString.Length - 1);
            String[] typeAndName = fieldString.Split(':');
            typeBuilder.DefineField(typeAndName[1], stringToType[typeAndName[0]], FieldAttributes.Public);
        }

        private void ProcessSection(String sectionString)
        {
            // Neue Section anhand des Namens erstellen.
            int indexOpenBracket = sectionString.IndexOf('[');
            int indexClosedBracket = sectionString.IndexOf(']');
            Section newSection = ParseSectionHeader(sectionString.Substring(indexOpenBracket, 
                indexClosedBracket - (indexOpenBracket - 1)));

            // Jedes SectionAttribute identifizieren und verarbeiten.
            List<String> sectionAttributes = sectionString.Remove(0, indexClosedBracket + 1).Split(';').ToList();
            sectionAttributes.RemoveAt(sectionAttributes.Count - 1);

            sectionAttributes.ForEach((attribute) =>
            {
                newSection.Attributes.Add(ProcessSectionAttribute(attribute));
            });

            AddSection(newSection);
        }

        private SectionAttribute ProcessSectionAttribute(String attributeString)
        {
            // String nach Typ, Name und Wert des Attributs auftrennen.
            String[] all = attributeString.Split('=');
            String[] typeAndName = all[0].Split(':');

            String type = typeAndName[0];
            String name = typeAndName[1];
            String valueString = all[1];

            // ValueString in objekt umwandeln.
            object value;
            if (!stringToEType.ContainsKey(type))
            {
                value = StringToValue(valueString, type, true);
                return new SectionAttribute(name, value, EType.USER_DEFINED);
            }
            else
            {
                value = StringToValue(valueString, type, false);
                return new SectionAttribute(name, value, stringToEType[type]);
            }

        }

        private void ProcessInclude(String includeString)
        {
            String includeFile = GetStringInsideBrackets(includeString);
            String includeFileContents = File.ReadAllText(includeFile);

            ParseFileContents(includeFileContents);
        }

        private String GetStringInsideBrackets(String str)
        {
            int openBracketIndex = str.IndexOf("[");
            int closingBracketIndex = str.IndexOf("]");

            return str.Substring(openBracketIndex + 1, closingBracketIndex - (openBracketIndex + 1));
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

        //private SectionAttribute ParseSectionAttribute(String attributeString, Section currentSection)
        //{
        //    int numDoubleColons = attributeString.Count(c => c == ':'); ;
        //    int numEquals = attributeString.Count(c => c == '=');
        //    int numSemicolons = attributeString.Count(c => c == ';');

        //    if (numDoubleColons != 1 || numEquals != 1 || numSemicolons != 1)
        //    {
        //        if (currentSection.Attributes.Count == 0)
        //        {
        //            throw new ArgumentException("Syntax Error in first SectionAttribute in Section \"" + currentSection.Name + "\".");
        //        }
        //        else
        //        {
        //            throw new ArgumentException("Syntax Error in SectionAttribute after \"" + currentSection.Attributes[currentSection.Attributes.Count - 1].Name +
        //                                    "\" in Section \"" + currentSection.Name + "\".");
        //        }
        //    }

        //    EType type;
        //    String name;
        //    Object value;

        //    // TODO: Am Anfang der Methode wird bereits nach diesen Zeichen gesucht. Das sollte man
        //    //       insgesamt nur einmal machen.
        //    int indexDoubleColon = attributeString.IndexOf(':');
        //    int indexEquals = attributeString.IndexOf('=');
        //    int indexSemicolon = attributeString.IndexOf(';');

        //    String typeString = attributeString.Substring(0, indexDoubleColon);
        //    name = attributeString.Substring(indexDoubleColon + 1, indexEquals - (indexDoubleColon + 1));
        //    String valueString = attributeString.Substring(indexEquals + 1, indexSemicolon - (indexEquals + 1));

        //    if (!stringToType.TryGetValue(typeString, out type))
        //    {
        //        throw new ArgumentException("Given type \"" + typeString + "\" does not exist.");
        //    }

        //    value = StringToValue(valueString, type);

        //    return new SectionAttribute(name, value, type);
        //}

        private object StringToValue_UserDefined(String valueString, String type)
        {
            List<String> valueStrings = new List<String>();
            int bracketCounter = 1;
            int charIndex = 1;
            char currentChar;
            String nextValueString = String.Empty;
            while(bracketCounter > 0)
            {
                currentChar = valueString[charIndex++];

                if (currentChar == '{')
                {
                    if (bracketCounter >= 1)
                        nextValueString += '{';
                    ++bracketCounter;
                }
                else if (currentChar == '}')
                {
                    if (bracketCounter > 1)
                        nextValueString += '}';
                    --bracketCounter;

                    if (bracketCounter == 0)
                        valueStrings.Add(nextValueString);
                }
                else if (bracketCounter > 1)
                    nextValueString += currentChar;
                else if (currentChar != ',')
                    nextValueString += currentChar;
                else
                {
                    valueStrings.Add(nextValueString);
                    nextValueString = String.Empty;
                }
            }
            object[] valueObjects = new object[valueStrings.Count];

            UserDefinedType udt = userDefinedTypes[type];

            for (int i = 0; i < valueStrings.Count; ++i)
            {
                valueObjects[i] = StringToValue(valueStrings[i], udt.subtypes[i].Item1,
                    udt.subtypes[i].Item2);
            }

            return Activator.CreateInstance(udt.type, valueObjects);
        }

        private object StringToValue(String valueString, String type, bool isUserDefined)
        {
            object returnValue = null;

            if (isUserDefined)
            {
                returnValue = StringToValue_UserDefined(valueString, type);
            }
            else
            {
                if (type.Contains("List<List"))
                {
                    returnValue = List2dStringToValue(valueString, type);
                }
                else if (type.Contains("List"))
                {
                    returnValue = ListStringToValue(valueString, type);
                }
                else
                {
                    switch(type)
                    {
                        case "bool": { returnValue = StringToBool(valueString); break; }
                        case "int": { returnValue = StringToInt(valueString); break; }
                        case "float": { returnValue = StringToFloat(valueString); break; }
                        case "double": { returnValue = StringToDouble(valueString); break; }
                        case "string": { returnValue = StringToString(valueString); break; }
                    }
                }
            }

            return returnValue;
        }

        //private Object StringToValue_OLD(String valueString, EType type)
        //{
        //    // Wie geht das mit den benutzerdefinierten Typen?
        //    // Jeder von denen hat einen speziellen EType, den ich 
        //    // hier im Code aber vorher nicht weiß.
        //    // Ein EType für benutzerdefinierte Typen ?
        //    // Vom valueString selbst ist der Typ nicht ablesbar,
        //    // dieser muss also als String zur Laufzeit übergeben werden.

        //    if (valueString == "NULL")
        //    {
        //        return null;
        //    }

        //    Object returnValue;

        //    if (type == EType.LIST_BOOL ||
        //        type == EType.LIST_INT ||
        //        type == EType.LIST_FLOAT ||
        //        type == EType.LIST_DOUBLE ||
        //        type == EType.LIST_STRING ||
        //        type == EType.LIST_VECTOR2 ||
        //        type == EType.LIST_RECTANGLE)
        //    {
        //        returnValue = ListStringToValue(valueString, type);
        //    }
        //    else if (type == EType.LIST_LIST_BOOL ||
        //             type == EType.LIST_LIST_INT ||
        //             type == EType.LIST_LIST_FLOAT ||
        //             type == EType.LIST_LIST_DOUBLE ||
        //             type == EType.LIST_LIST_STRING ||
        //             type == EType.LIST_LIST_VECTOR2 ||
        //             type == EType.LIST_LIST_RECTANGLE)
        //    {
        //        returnValue = List2dStringToValue(valueString, type);
        //    }
        //    else
        //    {
        //        switch (type)
        //        {
        //            case EType.BOOL:
        //                returnValue = StringToBool(valueString);
        //                break;

        //            case EType.INT:
        //                returnValue = StringToInt(valueString);
        //                break;

        //            case EType.FLOAT:
        //                returnValue = StringToFloat(valueString);
        //                break;

        //            case EType.DOUBLE:
        //                returnValue = StringToDouble(valueString);
        //                break;

        //            case EType.STRING:
        //                returnValue = StringToString(valueString);
        //                break;

        //            case EType.SECTION:
        //                returnValue = StringToSection(valueString);
        //                break;

        //            default:
        //                throw new ArgumentException("Given type \"" + type.ToString() + "\" is not supported.");
        //        }
        //    }

        //    return returnValue;
        //}

        private object ListStringToValue(String listString, String type)
        {
            object returnValue = null;

            bool emptyList = false;
            if (listString == "{}")
                emptyList = true;

            // Remove '{' and '}'
            listString = listString.Remove(0, 1);
            listString = listString.Remove(listString.Length - 1, 1);

            String[] elements = listString.Split(',');

            switch (type)
            {
                case "List<bool>":
                    {
                        if (emptyList)
                            return new List<bool>();

                        List<bool> list = new List<bool>();
                        foreach (String boolString in elements)
                            list.Add(StringToBool(boolString));
                        returnValue = list;

                        break;
                    }

                case "List<int>":
                    {
                        if (emptyList)
                            return new List<int>();

                        List<int> list = new List<int>();
                        foreach (String intString in elements)
                            list.Add(StringToInt(intString));
                        returnValue = list;

                        break;
                    }

                case "List<float>":
                    {
                        if (emptyList)
                            return new List<float>();

                        List<float> list = new List<float>();
                        foreach (String floatString in elements)
                            list.Add(StringToFloat(floatString));
                        returnValue = list;

                        break;
                    }

                case "List<double>":
                    {
                        if (emptyList)
                            return new List<double>();

                        List<double> list = new List<double>();
                        foreach (String doubleString in elements)
                            list.Add(StringToDouble(doubleString));
                        returnValue = list;

                        break;
                    }

                case "List<string>":
                    {
                        if (emptyList)
                            return new List<string>();

                        // TODO: Dämliche Bennenung StringToString und stringString.
                        List<string> list = new List<string>();
                        foreach (String stringString in elements)
                            list.Add(StringToString(stringString));
                        returnValue = list;

                        break;
                    }

                default:
                    throw new ArgumentException("Given type \"" + type.ToString() + "\" is not a supported list type.");
            }

            return returnValue;
        }

        //private Object ListStringToValue_OLD(String listString, EType type)
        //{
        //    Object returnValue = null;

        //    bool emptyList = false;
        //    if (listString == "{}")
        //    {
        //        emptyList = true;
        //    }

        //    // Remove '{' and '}'
        //    listString = listString.Remove(0, 1);
        //    listString = listString.Remove(listString.Length - 1, 1);

        //    String[] elements;
        //    if (type == EType.LIST_RECTANGLE ||
        //        type == EType.LIST_VECTOR2)
        //    {
        //        elements = listString.Split(new String[] { ")," }, StringSplitOptions.None);
        //        for (int i = 0; i < elements.Length - 1; ++i)
        //        {
        //            elements[i] += ")";
        //        }
        //    }
        //    else
        //    {
        //        elements = listString.Split(',');
        //    }

        //    switch (type)
        //    {
        //        case EType.LIST_BOOL:
        //            {
        //                if (emptyList)
        //                    return new List<bool>();

        //                List<bool> list = new List<bool>();
        //                foreach (String boolString in elements)
        //                {
        //                    list.Add(StringToBool(boolString));
        //                }
        //                returnValue = list;

        //                break;
        //            }

        //        case EType.LIST_INT:
        //            {
        //                if (emptyList)
        //                    return new List<int>();

        //                List<int> list = new List<int>();
        //                foreach (String intString in elements)
        //                {
        //                    list.Add(StringToInt(intString));
        //                }
        //                returnValue = list;

        //                break;
        //            }

        //        case EType.LIST_FLOAT:
        //            {
        //                if (emptyList)
        //                    return new List<float>();

        //                List<float> list = new List<float>();
        //                foreach (String floatString in elements)
        //                {
        //                    list.Add(StringToFloat(floatString));
        //                }
        //                returnValue = list;

        //                break;
        //            }

        //        case EType.LIST_DOUBLE:
        //            {
        //                if (emptyList)
        //                    return new List<double>();

        //                List<double> list = new List<double>();
        //                foreach (String doubleString in elements)
        //                {
        //                    list.Add(StringToDouble(doubleString));
        //                }
        //                returnValue = list;

        //                break;
        //            }

        //        case EType.LIST_STRING:
        //            {
        //                if (emptyList)
        //                    return new List<string>();

        //                // TODO: Dämliche Bennenung StringToString und stringString.
        //                List<string> list = new List<string>();
        //                foreach (String stringString in elements)
        //                {
        //                    list.Add(StringToString(stringString));
        //                }
        //                returnValue = list;

        //                break;
        //            }

        //        default:
        //            throw new ArgumentException("Given type \"" + type.ToString() + "\" is not a supported list type.");
        //    }

        //    return returnValue;
        //}

        private object List2dStringToValue(String s, String type)
        {
            object returnValue = null;

            s = s.Remove(0, 1);
            s = s.Remove(s.Length - 1, 1);

            String[] elements = s.Split(new String[] { "}," }, StringSplitOptions.None);

            for (int i = 0; i < elements.Length - 1; ++i)
                elements[i] += "}";

            switch (type)
            {
                case "List<List<bool>>":
                    {
                        List<List<bool>> listOfLists = new List<List<bool>>();
                        foreach (String listString in elements)
                            listOfLists.Add((List<bool>)ListStringToValue(listString, "List<bool>"));
                        returnValue = listOfLists;

                        break;
                    }
                case "List<List<int>>":
                    {
                        List<List<int>> listOfLists = new List<List<int>>();
                        foreach (String listString in elements)
                            listOfLists.Add((List<int>)ListStringToValue(listString, "List<int>"));
                        returnValue = listOfLists;

                        break;
                    }
                case "List<List<float>>":
                    {
                        List<List<float>> listOfLists = new List<List<float>>();
                        foreach (String listString in elements)
                            listOfLists.Add((List<float>)ListStringToValue(listString, "List<float>"));
                        returnValue = listOfLists;

                        break;
                    }
                case "List<List<double>>":
                    {
                        List<List<double>> listOfLists = new List<List<double>>();
                        foreach (String listString in elements)
                        {
                            listOfLists.Add((List<double>)ListStringToValue(listString, "List<double>"));
                        }
                        returnValue = listOfLists;

                        break;
                    }
                case "List<List<string>>":
                    {
                        List<List<string>> listOfLists = new List<List<string>>();
                        foreach (String listString in elements)
                        {
                            listOfLists.Add((List<string>)ListStringToValue(listString, "List<string>"));
                        }
                        returnValue = listOfLists;

                        break;
                    }

                default:
                    throw new ArgumentException("Given type \"" + type.ToString() + "\" is not a supported list list type.");
            }

            return returnValue;
        }

        //private Object List2dStringToValue_OLD(String s, EType type)
        //{
        //    Object returnValue = null;

        //    s = s.Remove(0, 1);
        //    s = s.Remove(s.Length - 1, 1);

        //    String[] elements = s.Split(new String[] { "}," }, StringSplitOptions.None);

        //    for (int i = 0; i < elements.Length - 1; ++i)
        //    {
        //        elements[i] += "}";
        //    }

        //    switch (type)
        //    {
        //        case EType.LIST_LIST_BOOL:
        //            {
        //                List<List<bool>> listOfLists = new List<List<bool>>();
        //                foreach (String listString in elements)
        //                {
        //                    listOfLists.Add((List<bool>)ListStringToValue(listString, EType.LIST_BOOL));
        //                }
        //                returnValue = listOfLists;

        //                break;
        //            }
        //        case EType.LIST_LIST_INT:
        //            {
        //                List<List<int>> listOfLists = new List<List<int>>();
        //                foreach (String listString in elements)
        //                {
        //                    listOfLists.Add((List<int>)ListStringToValue(listString, EType.LIST_INT));
        //                }
        //                returnValue = listOfLists;

        //                break;
        //            }
        //        case EType.LIST_LIST_FLOAT:
        //            {
        //                List<List<float>> listOfLists = new List<List<float>>();
        //                foreach (String listString in elements)
        //                {
        //                    listOfLists.Add((List<float>)ListStringToValue(listString, EType.LIST_FLOAT));
        //                }
        //                returnValue = listOfLists;

        //                break;
        //            }
        //        case EType.LIST_LIST_DOUBLE:
        //            {
        //                List<List<double>> listOfLists = new List<List<double>>();
        //                foreach (String listString in elements)
        //                {
        //                    listOfLists.Add((List<double>)ListStringToValue(listString, EType.LIST_DOUBLE));
        //                }
        //                returnValue = listOfLists;

        //                break;
        //            }
        //        case EType.LIST_LIST_STRING:
        //            {
        //                List<List<string>> listOfLists = new List<List<string>>();
        //                foreach (String listString in elements)
        //                {
        //                    listOfLists.Add((List<string>)ListStringToValue(listString, EType.LIST_STRING));
        //                }
        //                returnValue = listOfLists;

        //                break;
        //            }

        //        default:
        //            throw new ArgumentException("Given type \"" + type.ToString() + "\" is not a supported list list type.");
        //    }

        //    return returnValue;
        //}

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

        /// <summary>
        /// Returns the corresponding Section of a SectionAttribute's value.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public Section StringToSection(String s)
        {
            Section existingSection = sections.Find(section => section.Name == s);
            if (existingSection == null)
            {
                throw new ArgumentException("There is no Section \"" + s + "\".");
            }

            return existingSection;
        }

        #endregion
    }
}
