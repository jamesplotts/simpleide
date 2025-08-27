' CodeSenseContext.vb - Enhanced CodeSense context using node graph information
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Syntax
Imports SimpleIDE.Utilities

Namespace Models
    
    ' Enhanced CodeSense context with node graph information
    Public Class CodeSenseContext
        
        ' Basic context (from IEditor interface)
        Public Property TriggerPosition As EditorPosition
        Public Property TriggerChar As Char
        Public Property TriggerKind As CodeSenseTriggerKind
        Public Property CurrentWord As String
        Public Property LineText As String
        Public Property FileType As String
        
        ' Enhanced context from node graph
        Public Property CurrentScope As String              ' full Scope Path (e.g., "MyClass.MyMethod")
        Public Property ContainingClass As String          ' Name of containing class/module
        Public Property ContainingMethod As String         ' Name of containing method/function
        Public Property AvailableMembers As List(Of String) ' members available in current class
        Public Property LocalVariables As List(Of String)   ' Variables in current Scope
        Public Property MemberAccessTarget As String       ' Identifier before dot (for member access)
        
        ' Type information (for advanced CodeSense)
        Public Property ExpectedReturnType As String       ' Expected return Type in current Context
        Public Property ParameterContext As ParameterInfo  ' Parameter information if in method call
        
        ' Navigation context
        Public Property CanNavigateToDefinition As Boolean ' Whether "Go to definition" is available
        Public Property DefinitionLocation As EditorPosition ' Location of definition if available
        
        ' Suggestions based on context
        Public Property SuggestedCompletions As List(Of CompletionItem)
        Public Property SuggestedActions As List(Of CodeAction)

        Private pWordStartOffset As Integer
        Private pPrefix As String
        Private pCurrentLine As String
        Private pLineNumber As Integer
        Private pColumn As Integer
 
        
        Public Sub New()
            AvailableMembers = New List(Of String)()
            LocalVariables = New List(Of String)()
            SuggestedCompletions = New List(Of CompletionItem)()
            SuggestedActions = New List(Of CodeAction)()
        End Sub
        
        ' Check if we're in a specific context
        Public ReadOnly Property IsInClassContext As Boolean
            Get
                Return Not String.IsNullOrEmpty(ContainingClass)
            End Get
        End Property
        
        Public ReadOnly Property IsInMethodContext As Boolean
            Get
                Return Not String.IsNullOrEmpty(ContainingMethod)
            End Get
        End Property
        
        Public ReadOnly Property IsMemberAccess As Boolean
            Get
                Return TriggerChar = "."c AndAlso Not String.IsNullOrEmpty(MemberAccessTarget)
            End Get
        End Property
        
        Public ReadOnly Property IsParameterContext As Boolean
            Get
                Return ParameterContext IsNot Nothing
            End Get
        End Property

        ' Word start offset in the document
        Public Property WordStartOffset As Integer
            Get
                Return pWordStartOffset
            End Get
            Set(Value As Integer)
                pWordStartOffset = Value
            End Set
        End Property
        
        ' Text before the current word on the same line
        Public Property Prefix As String
            Get
                Return If(pPrefix, "")
            End Get
            Set(Value As String)
                pPrefix = Value
            End Set
        End Property
        
        ' Current line text (alias for LineText for compatibility)
        Public Property CurrentLine As String
            Get
                Return LineText
            End Get
            Set(Value As String)
                LineText = Value
            End Set
        End Property
        
        ' Line number (extracted from TriggerPosition)
        Public Property LineNumber As Integer
            Get
                Return TriggerPosition.Line
            End Get
            Set(Value As Integer)
                ' Update TriggerPosition with new line
                TriggerPosition = New EditorPosition(Value, TriggerPosition.Column)
            End Set
        End Property
        
        ' Column number (extracted from TriggerPosition)
        Public Property Column As Integer
            Get
                Return TriggerPosition.Column
            End Get
            Set(Value As Integer)
                ' Update TriggerPosition with new column
                TriggerPosition = New EditorPosition(TriggerPosition.Line, Value)
            End Set
        End Property
        
    End Class
    
    
    ' Individual parameter information
    Public Class Parameter
        Public Property Name As String
        Public Property Type As String
        Public Property IsOptional As Boolean
        Public Property DefaultValue As String
        Public Property Description As String
    End Class
    
    ' Code completion item
    Public Class CompletionItem
        Public Property Text As String              ' the Text to insert
        Public Property DisplayText As String       ' Text to Show in completion list
        Public Property Description As String       ' Detailed Description
        Public Property Kind As CompletionKind      ' What Kind of item this is
        Public Property Priority As Integer         ' Higher Priority items shown first
        Public Property InsertionPoint As EditorPosition ' Where to insert this item
        Public Property Icon As String = ""             ' Icon Identifier   
     
        Public Sub New(vText As String, vKind As CompletionKind)
            Text = vText
            DisplayText = vText
            Kind = vKind
            Priority = 0
        End Sub

        Public Sub New()

        End Sub
        
        Public Sub New(vText As String, vDisplayText As String, vKind As CompletionKind, vDescription As String)
            Text = vText
            DisplayText = vDisplayText
            Kind = vKind
            Description = vDescription
            Priority = 0
        End Sub
    End Class
    
    ' Types of completion items
    Public Enum CompletionKind
        eUnspecified
        eKeyword            ' VB.NET Keywords
        eClass              ' Class names
        eMethod             ' Methods/Subs
        eFunction           ' Functions
        eProperty           ' Properties
        eField              ' Fields/Variables
        eEvent              ' Events
        eNamespace          ' Namespaces
        eInterface          ' Interfaces
        eEnum               ' Enumerations
        eSnippet            ' code snippets
        eLocalVariable      ' Local variables
        eParameter          ' Method Parameters
        eLastValue
    End Enum
    
    ' Code action (refactoring, fixes, etc.)
    Public Class CodeAction
        Public Property Title As String             ' Action Title shown to user
        Public Property Description As String       ' Detailed Description
        Public Property Kind As CodeActionKind      ' What Kind of action
        Public Property IsPreferred As Boolean      ' Whether this is the preferred action
        Public Property Changes As List(Of TextChange) ' Changes to make
        
        Public Sub New(vTitle As String, vKind As CodeActionKind)
            Title = vTitle
            Kind = vKind
            Changes = New List(Of TextChange)()
        End Sub
    End Class
    
    ' Types of code actions
    Public Enum CodeActionKind
        eUnspecified
        eQuickFix           ' Fix compilation Errors
        eRefactorExtract    ' Extract method/variable
        eRefactorInline     ' Inline method/variable
        eRefactorRename     ' Rename symbol
        eRefactorRewrite    ' Rewrite code structure
        eSourceOrganize     ' Organize imports/usings
        eSourceFormat       ' Format code
        eLastValue
    End Enum
    
    ' Text change for code actions
    Public Class TextChange
        Public Property Range As TextRange         ' Range to Replace
        Public Property NewText As String          ' New Text to insert
        
        Public Sub New(vRange As TextRange, vNewText As String)
            Range = vRange
            NewText = vNewText
        End Sub
    End Class
    
    ' Text range for changes
    Public Structure TextRange
        Public StartPosition As EditorPosition
        Public EndPosition As EditorPosition
        
        Public Sub New(vStart As EditorPosition, vEnd As EditorPosition)
            StartPosition = vStart
            EndPosition = vEnd
        End Sub
        
        Public ReadOnly Property IsEmpty As Boolean
            Get
                Return StartPosition.Line = EndPosition.Line AndAlso StartPosition.Column = EndPosition.Column
            End Get
        End Property
    End Structure
    
End Namespace
