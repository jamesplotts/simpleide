 
' {FileName}.vb - Structure definition
' Created: {CreatedDate}
Imports System
Imports System.Collections.Generic

{NamespaceDeclaration}Public Structure {StructureName}
    
    ' ===== Public Fields =====
    ' NOTE: Structures typically use public fields
    ' NOTE: Use PascalCase for field names
    ' Example: Public MyField As String
    ' Example: Public MyNumber As Integer
    
    ' ===== Properties =====
    ' NOTE: Can also use properties in structures
    ' Example:
    ' Public Property MyProperty As String
    '     Get
    '         Return pMyField
    '     End Get
    '     Set(value As String)
    '         pMyField = value
    '     End Set
    ' End Property
    
    ' ===== Constructor =====
    ' NOTE: Structures can have parameterized constructors
    ' Example:
    ' Public Sub New(vMyField As String, vMyNumber As Integer)
    '     MyField = vMyField
    '     MyNumber = vMyNumber
    ' End Sub
    
    ' ===== Methods =====
    ' NOTE: Use PascalCase for method names
    ' NOTE: Use v prefix for parameters
    ' NOTE: Use l prefix for local variables
    ' Example:
    ' Public Function ToString() As String
    '     Return $"{MyField}: {MyNumber}"
    ' End Function
    
    ' ===== Operators =====
    ' NOTE: Can override operators for structures
    ' Example:
    ' Public Shared Operator =(vLeft As {StructureName}, vRight As {StructureName}) As Boolean
    '     Return vLeft.MyField = vRight.MyField AndAlso vLeft.MyNumber = vRight.MyNumber
    ' End Operator
    
    ' Public Shared Operator <>(vLeft As {StructureName}, vRight As {StructureName}) As Boolean
    '     Return Not (vLeft = vRight)
    ' End Operator
    
End Structure{NamespaceEnd}
