' Models/ProjectNodeType.vb

Namespace Models
    
    ''' <summary>
    ''' Specifies the type of node in a project tree
    ''' </summary>
    Public Enum ProjectNodeType
        ''' <summary>Unknown or unspecified node type</summary>
        eUnspecified
        ''' <summary>Project root node</summary>
        eProject
        ''' <summary>Folder node</summary>
        eFolder
        ''' <summary>VB source file (.vb)</summary>
        eVBFile
        ''' <summary>XML file (.xml)</summary>
        eXMLFile
        ''' <summary>Text file (.txt)</summary>
        eTextFile
        ''' <summary>Configuration file (.config)</summary>
        eConfigFile
        ''' <summary>Resource file (.resx)</summary>
        eResourceFile
        ''' <summary>Image file</summary>
        eImageFile
        ''' <summary>References node (special folder)</summary>
        eReferences
        ''' <summary>Assembly manifest node</summary>
        eManifest
        ''' <summary>Resources folder (special folder)</summary>
        eResources
        ''' <summary>My Project folder (special folder)</summary>
        eMyProject
        ''' <summary>Sentinel value for enum bounds checking</summary>
        eLastValue
    End Enum
    
End Namespace