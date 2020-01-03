# ConfigFile
The ConfigFile is a Windows INI inspired file format for reading and writing arbitrary data using the C# Programming language. It is primary developed for a  MonoGame game project by the name of "ProjectSpace", developed by the SpaceRavers. <br />
The following gives an example for the ConfigFile file format...

[Category1: Section1] <br />
bool: someBoolean = true; <br />
int: someInt = 13; <br />
float: someFloat = 3.14; <br />
double: someDouble = 6.14; <br />
string: someString = "Hello World"; <br />
Rectangle: rec = (1, 2, 3, 4); <br />
Vector2: vec = (1, 2); <br />
List\<int\>: someListOfInts = {1, 2, 3, 4}; <br />
List\<List\<int\>\>: someListOfListsOfInts = {{1, 2, 3, 4}, {5, 6, 7, 8}}; <br />
<br />
[Category2: Section2] <br />
List\<List\<float\>\>: someListOfListsOfFloats = {{3.14, 3.5}, {5.4, 4.4}, {3.4, 4.44}}; <br />
List\<Vector2\>: vectors = {(12, 3), (3, 4), (30, 23), (40, 41)}

The following describes the concrete rules that the ConfigFile file format operates under... <br />
1. "[Category: Section]" is a SectionHeader. It marks the beginning of a Section. A Section is a set of SectionAttributes. A Section can, but doesn't have to, belong to a Category of Sections. Each Section's name is unique, so that we can get it by name. <br />
2. "bool: someBoolean = true;" is a SectionAttribute. It has a type (bool), a name (someBoolean), a value (true) and a Symbol that signals the end of the SectionAttribute (;). The semicolon is supposed to allow us to put parts of the SectionAttribute on different lines for better text formatting. <br />
3. bool, int, float, double, string are simple types. They represent built in types of C#. <br />
4. Rectangle is a composite(zusammengesetzt) type. Composite types are compositions of several simple types. <br />
5. List\<T\> is a list of values of type T. T can be any simple or composite type. <br />
6. List\<List\<T\>\> is a list of lists of values of type T. T can be any simple or composite type. <br />
