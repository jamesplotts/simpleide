' CodeSenseEngine.vb - Updated to work with ProjectParser instead of VBParser
' Migration Step 4: Remove VBParser dependencies and use ProjectParser structures
Imports System
Imports System.Collections.Generic
Imports System.Reflection
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities


Namespace Syntax
    
    
    ''' <summary>
    ''' Types of code completion triggers
    ''' </summary>
    Public Enum CodeSenseTriggerKind
        eUnspecified
        eDot            ' After "."
        eSpace          ' After space (contextual)
        eOpenParen      ' After "("
        eComma          ' After ","
        eManual         ' Ctrl+Space
        eLastValue
    End Enum
    
    ''' <summary>
    ''' CodeSense suggestion kind enum (for compatibility)
    ''' </summary>
    Public Enum CodeSenseSuggestionKind
        eUnspecified
        eKeyword
        eClass
        eInterface
        eMethod
        eProperty
        eField
        eEvent
        eNamespace
        eLocalVariable
        eParameter
        eSnippet
        eOther
        eLastValue
    End Enum
    
    ''' <summary>
    ''' Types of CodeSense suggestions
    ''' </summary>
    Public Enum CodeSenseSuggestionType
        eUnspecified
        eKeyword
        eType
        eNamespace
        eMethod
        eProperty
        eField
        eEvent
        eVariable
        eParameter
        eSnippet
        eOther
        eLastValue
    End Enum
    
    ''' <summary>
    ''' Provides intelligent code completion suggestions based on parsed document structure
    ''' </summary>
    Public Class CodeSenseEngine
        Implements IDisposable
        
        ' ===== Private Fields =====
        Private pProjectReferences As New List(Of Assembly)
        Private pCurrentSyntaxTree As SyntaxNode   ' Current document's syntax tree
        Private pProjectSyntaxTree As SyntaxNode   ' Full project syntax tree
        Private pTypeCache As New Dictionary(Of String, CodeSenseTypeInfo)
        Private pMemberCache As New Dictionary(Of String, List(Of MemberInfo))
        Private pKeywordSuggestions As List(Of CodeSenseSuggestion)
        Private pLastUpdateTime As DateTime = DateTime.MinValue
        Private pDisposed As Boolean = False
        Private pCurrentContext As CodeSenseContext ' Store current context for access in sub-methods
        
        ' ===== Events =====
        
        ''' <summary>
        ''' Raised when the engine needs access to the ProjectManager
        ''' </summary>
        Public Event ProjectManagerRequested(sender As Object, e As ProjectManagerRequestEventArgs)
        
        ' ===== Constructor =====
        
        ''' <summary>
        ''' Creates a new CodeSenseEngine instance
        ''' </summary>
        Public Sub New()
            Try
                ' Initialize keyword suggestions
                InitializeKeywordSuggestions()
                
                ' Load basic assemblies
                LoadCoreAssemblies()
                
                ' Subscribe to ProjectManager parse events if available
                SubscribeToProjectParseEvents()
                
            Catch ex As Exception
                Console.WriteLine($"CodeSenseEngine constructor error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Public Methods =====
        
        ''' <summary>
        ''' Updates from ProjectParser's SyntaxNode structure
        ''' </summary>
        ''' <param name="vSyntaxTree">The SyntaxNode tree from ProjectParser</param>
        ''' <param name="vIsProjectTree">True if this is the full project tree, False if just current document</param>
        Public Sub UpdateFromSyntaxTree(vSyntaxTree As SyntaxNode, Optional vIsProjectTree As Boolean = False)
            Try
                If vIsProjectTree Then
                    pProjectSyntaxTree = vSyntaxTree
                    Console.WriteLine($"CodeSenseEngine updated with project tree containing {CountNodes(vSyntaxTree)} nodes")
                Else
                    pCurrentSyntaxTree = vSyntaxTree
                    Console.WriteLine($"CodeSenseEngine updated with document tree containing {CountNodes(vSyntaxTree)} nodes")
                End If
                
                pLastUpdateTime = DateTime.Now
                
                ' Clear member cache as document structure changed
                pMemberCache.Clear()
                
            Catch ex As Exception
                Console.WriteLine($"UpdateFromSyntaxTree error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Updates from a ParseCompleted event from ProjectManager
        ''' </summary>
        ''' <param name="vFile">The parsed SourceFileInfo</param>
        ''' <param name="vResult">The parse result from ProjectParser</param>
        Public Sub UpdateFromParseResult(vFile As SourceFileInfo, vResult As Object)
            Try
                ' The result from ProjectParser.ParseContent should contain a SyntaxNode
                ' We need to extract it from the result object
                
                Dim lSyntaxNode As SyntaxNode = Nothing
                
                ' Check if result is directly a SyntaxNode
                lSyntaxNode = TryCast(vResult, SyntaxNode)
                
                ' If not directly a SyntaxNode, try to extract from a result object
                If lSyntaxNode Is Nothing AndAlso vResult IsNot Nothing Then
                    Dim lResultType = vResult.GetType()
                    
                    ' Try to get RootNode property (ProjectParser's ParseResult structure)
                    Dim lRootNodeProperty = lResultType.GetProperty("RootNode")
                    If lRootNodeProperty IsNot Nothing Then
                        lSyntaxNode = TryCast(lRootNodeProperty.GetValue(vResult), SyntaxNode)
                    End If
                    
                    ' If still nothing, check for SyntaxTree property
                    If lSyntaxNode Is Nothing Then
                        Dim lSyntaxTreeProperty = lResultType.GetProperty("SyntaxTree")
                        If lSyntaxTreeProperty IsNot Nothing Then
                            lSyntaxNode = TryCast(lSyntaxTreeProperty.GetValue(vResult), SyntaxNode)
                        End If
                    End If
                End If
                
                ' Update our syntax tree if we got a valid node
                If lSyntaxNode IsNot Nothing Then
                    ' Check if this is for the current document
                    Dim lIsCurrentDocument As Boolean = False
                    ' TODO: Determine if this is the current document based on vFile
                    
                    UpdateFromSyntaxTree(lSyntaxNode, False)
                    
                    Console.WriteLine($"CodeSenseEngine updated from parse result for {vFile.FileName}")
                End If
                
                ' Also try to get the full project tree from ProjectManager
                Dim lProjectManager = GetProjectManager()
                If lProjectManager IsNot Nothing Then
                    Dim lProjectTree = lProjectManager.ProjectSyntaxTree
                    If lProjectTree IsNot Nothing Then
                        UpdateFromSyntaxTree(lProjectTree, True)
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateFromParseResult error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Updates the document nodes from ProjectParser's syntax tree
        ''' </summary>
        ''' <param name="vRootNode">The root syntax node from ProjectParser</param>
        ''' <remarks>
        ''' This method now accepts ProjectParser's SyntaxNode structure directly
        ''' instead of VBParser.ParseResult nodes
        ''' </remarks>
        Public Sub UpdateDocumentNodes(vRootNode As SyntaxNode)
            Try
                If vRootNode Is Nothing Then Return
                
                ' Update the current syntax tree
                pCurrentSyntaxTree = vRootNode
                
                Console.WriteLine($"CodeSenseEngine updated document nodes from ProjectParser")
                
            Catch ex As Exception
                Console.WriteLine($"UpdateDocumentNodes error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Builds a flat list of nodes from ProjectParser's hierarchical structure
        ''' </summary>
        ''' <param name="vNode">The node to process</param>
        ''' <param name="vList">The list to add nodes to</param>
        Private Sub BuildNodeList(vNode As SyntaxNode, vList As List(Of SyntaxNode))
            Try
                If vNode Is Nothing OrElse vList Is Nothing Then Return
                
                ' Add the node to the list
                vList.Add(vNode)
                
                ' Process children recursively
                for each lChild in vNode.Children
                    BuildNodeList(lChild, vList)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"BuildNodeList error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Gets suggestions for a specific type using ProjectParser's nodes
        ''' </summary>
        ''' <param name="vTypeName">The type name to get suggestions for</param>
        ''' <returns>List of suggestions from ProjectParser data</returns>
        Private Function GetSuggestionsForType(vTypeName As String) As List(Of CodeSenseSuggestion)
            Try
                Dim lSuggestions As New List(Of CodeSenseSuggestion)()
                
                ' Build a temporary symbol table from current project tree
                If pProjectSyntaxTree IsNot Nothing Then
                    Dim lSymbolTable As New Dictionary(Of String, SyntaxNode)()
                    ProcessProjectNode(pProjectSyntaxTree, lSymbolTable)
                    
                    ' Look up type in project symbols
                    If lSymbolTable.ContainsKey(vTypeName) Then
                        Dim lTypeNode As SyntaxNode = lSymbolTable(vTypeName)
                        
                        ' Add all members of the type
                        for each lMember in lTypeNode.Children
                            If IsAccessibleMember(lMember) Then
                                lSuggestions.Add(CreateSuggestionFromNode(lMember))
                            End If
                        Next
                    End If
                End If
                
                ' Also check framework types using reflection
                lSuggestions.AddRange(GetFrameworkTypeSuggestions(vTypeName))
                
                Return lSuggestions
                
            Catch ex As Exception
                Console.WriteLine($"GetSuggestionsForType error: {ex.Message}")
                Return New List(Of CodeSenseSuggestion)()
            End Try
        End Function

        ''' <summary>
        ''' Checks if a member is accessible in the current context
        ''' </summary>
        Private Function IsAccessibleMember(vNode As SyntaxNode) As Boolean
            Try
                ' For now, return true for public and friend members
                Return vNode.IsPublic OrElse vNode.IsFriend
                
            Catch ex As Exception
                Console.WriteLine($"IsAccessibleMember error: {ex.Message}")
                Return True
            End Try
        End Function

        ''' <summary>
        ''' Gets suggestions for a framework type using reflection
        ''' </summary>
        Private Function GetFrameworkTypeSuggestions(vTypeName As String) As List(Of CodeSenseSuggestion)
            Try
                Dim lSuggestions As New List(Of CodeSenseSuggestion)()
                
                ' Try to find type in loaded assemblies
                for each lAssembly in pProjectReferences
                    Try
                        Dim lType As Type = lAssembly.GetType(vTypeName, False, True)
                        If lType Is Nothing Then
                            ' Try with System namespace
                            lType = lAssembly.GetType($"System.{vTypeName}", False, True)
                        End If
                        
                        If lType IsNot Nothing Then
                            ' Add methods
                            for each lMethod in lType.GetMethods(Reflection.BindingFlags.Public Or Reflection.BindingFlags.Instance)
                                If Not lMethod.IsSpecialName Then
                                    lSuggestions.Add(CreateSuggestionFromMemberInfo(lMethod))
                                End If
                            Next
                            
                            ' Add properties
                            for each lProperty in lType.GetProperties(Reflection.BindingFlags.Public Or Reflection.BindingFlags.Instance)
                                lSuggestions.Add(CreateSuggestionFromMemberInfo(lProperty))
                            Next
                            
                            Exit for ' Found the type, no need to continue
                        End If
                    Catch
                        ' Ignore assembly load errors
                    End Try
                Next
                
                Return lSuggestions
                
            Catch ex As Exception
                Console.WriteLine($"GetFrameworkTypeSuggestions error: {ex.Message}")
                Return New List(Of CodeSenseSuggestion)()
            End Try
        End Function

        ''' <summary>
        ''' Creates a CodeSense suggestion from reflection MemberInfo
        ''' </summary>
        Private Function CreateSuggestionFromMemberInfo(vMember As MemberInfo) As CodeSenseSuggestion
            Try
                Dim lSuggestion As New CodeSenseSuggestion()
                
                lSuggestion.Text = vMember.Name
                lSuggestion.DisplayText = vMember.Name
                
                Select Case vMember.MemberType
                    Case MemberTypes.Method
                        lSuggestion.Kind = CodeSenseSuggestionKind.eMethod
                        lSuggestion.Icon = "method"
                        Dim lMethod As MethodInfo = DirectCast(vMember, MethodInfo)
                        lSuggestion.Signature = BuildMethodSignatureFromReflection(lMethod)
                        
                    Case MemberTypes.Property
                        lSuggestion.Kind = CodeSenseSuggestionKind.eProperty
                        lSuggestion.Icon = "property"
                        
                    Case MemberTypes.Field
                        lSuggestion.Kind = CodeSenseSuggestionKind.eField
                        lSuggestion.Icon = "field"
                        
                    Case MemberTypes.Event
                        lSuggestion.Kind = CodeSenseSuggestionKind.eEvent
                        lSuggestion.Icon = "event"
                        
                    Case Else
                        lSuggestion.Kind = CodeSenseSuggestionKind.eOther
                        lSuggestion.Icon = "member"
                End Select
                
                Return lSuggestion
                
            Catch ex As Exception
                Console.WriteLine($"CreateSuggestionFromMemberInfo error: {ex.Message}")
                Return New CodeSenseSuggestion() with {.Text = vMember?.Name}
            End Try
        End Function

        ''' <summary>
        ''' Builds a method signature string from MethodInfo
        ''' </summary>
        Private Function BuildMethodSignatureFromReflection(vMethod As MethodInfo) As String
            Try
                Dim lParams As New List(Of String)()
                
                for each lParam in vMethod.GetParameters()
                    lParams.Add($"{lParam.Name} As {lParam.ParameterType.Name}")
                Next
                
                Dim lSignature As String = $"{vMethod.Name}({String.Join(", ", lParams)})"
                
                If vMethod.ReturnType IsNot GetType(Void) Then
                    lSignature &= $" As {vMethod.ReturnType.Name}"
                End If
                
                Return lSignature
                
            Catch ex As Exception
                Console.WriteLine($"BuildMethodSignatureFromReflection error: {ex.Message}")
                Return vMethod?.Name
            End Try
        End Function

        ''' <summary>
        ''' Creates a CodeSense suggestion from a ProjectParser SyntaxNode
        ''' </summary>
        ''' <param name="vNode">The node to create suggestion from</param>
        ''' <returns>A CodeSenseSuggestion object</returns>
        Private Function CreateSuggestionFromNode(vNode As SyntaxNode) As CodeSenseSuggestion
            Try
                Dim lSuggestion As New CodeSenseSuggestion()
                
                ' Set basic properties from ProjectParser node
                lSuggestion.Name = vNode.Name
                lSuggestion.DisplayText = vNode.Name
                lSuggestion.Text = vNode.Name
                
                ' Set kind based on node type
                Select Case vNode.NodeType
                    Case CodeNodeType.eMethod, CodeNodeType.eFunction
                        lSuggestion.Kind = CodeSenseSuggestionKind.eMethod
                        lSuggestion.Icon = "method"
                        lSuggestion.SuggestionType = CodeSenseSuggestionType.eMethod
                        
                    Case CodeNodeType.eProperty
                        lSuggestion.Kind = CodeSenseSuggestionKind.eProperty
                        lSuggestion.Icon = "property"
                        lSuggestion.SuggestionType = CodeSenseSuggestionType.eProperty
                        
                    Case CodeNodeType.eField
                        lSuggestion.Kind = CodeSenseSuggestionKind.eField
                        lSuggestion.Icon = "field"
                        lSuggestion.SuggestionType = CodeSenseSuggestionType.eField
                        
                    Case CodeNodeType.eEvent
                        lSuggestion.Kind = CodeSenseSuggestionKind.eEvent
                        lSuggestion.Icon = "event"
                        lSuggestion.SuggestionType = CodeSenseSuggestionType.eEvent
                        
                    Case CodeNodeType.eClass
                        lSuggestion.Kind = CodeSenseSuggestionKind.eClass
                        lSuggestion.Icon = "class"
                        lSuggestion.SuggestionType = CodeSenseSuggestionType.eType
                        
                    Case CodeNodeType.eInterface
                        lSuggestion.Kind = CodeSenseSuggestionKind.eInterface
                        lSuggestion.Icon = "interface"
                        lSuggestion.SuggestionType = CodeSenseSuggestionType.eType
                        
                    Case CodeNodeType.eEnum
                        lSuggestion.Kind = CodeSenseSuggestionKind.eOther  ' No eEnum in current enum
                        lSuggestion.Icon = "enum"
                        lSuggestion.SuggestionType = CodeSenseSuggestionType.eType
                        
                    Case Else
                        lSuggestion.Kind = CodeSenseSuggestionKind.eKeyword
                        lSuggestion.Icon = "keyword"
                        lSuggestion.SuggestionType = CodeSenseSuggestionType.eOther
                End Select
                
                ' Add documentation summary if available
                If vNode.XmlDocumentation IsNot Nothing Then
                    lSuggestion.Description = vNode.XmlDocumentation.Summary
                End If
                
                ' Add signature for methods
                If vNode.NodeType = CodeNodeType.eMethod OrElse vNode.NodeType = CodeNodeType.eFunction Then
                    lSuggestion.Signature = BuildMethodSignatureFromNode(vNode)
                End If
                
                Return lSuggestion
                
            Catch ex As Exception
                Console.WriteLine($"CreateSuggestionFromNode error: {ex.Message}")
                Return New CodeSenseSuggestion() with {.Name = vNode?.Name, .Text = vNode?.Name}
            End Try
        End Function

        ''' <summary>
        ''' Builds a method signature string from a SyntaxNode
        ''' </summary>
        Private Function BuildMethodSignatureFromNode(vNode As SyntaxNode) As String
            Try
                If vNode Is Nothing Then Return ""
                
                Dim lSignature As String = vNode.Name & "("
                
                ' Add parameters if available
                Dim lParams As New List(Of String)()
                for each lChild in vNode.Children
                    If lChild.NodeType = CodeNodeType.eParameter Then
                        Dim lParamStr As String = lChild.Name
                        If Not String.IsNullOrEmpty(lChild.DataType) Then
                            lParamStr &= " As " & lChild.DataType
                        End If
                        lParams.Add(lParamStr)
                    End If
                Next
                
                lSignature &= String.Join(", ", lParams) & ")"
                
                ' Add return type if available
                If Not String.IsNullOrEmpty(vNode.DataType) Then
                    lSignature &= " As " & vNode.DataType
                End If
                
                Return lSignature
                
            Catch ex As Exception
                Console.WriteLine($"BuildMethodSignatureFromNode error: {ex.Message}")
                Return vNode?.Name & "()"
            End Try
        End Function

        ''' <summary>
        ''' Updates CodeSense with the complete project structure from ProjectParser
        ''' </summary>
        ''' <param name="vProjectTree">The project syntax tree from ProjectParser</param>
        ''' <remarks>
        ''' This method processes the entire project structure from ProjectManager's
        ''' centralized ProjectParser to build a comprehensive symbol index
        ''' </remarks>
        Public Sub UpdateFromProjectStructure(vProjectTree As SyntaxNode)
            Try
                If vProjectTree Is Nothing Then Return
                
                ' Update the project syntax tree
                UpdateFromSyntaxTree(vProjectTree, True)
                
                Console.WriteLine($"CodeSenseEngine updated from project structure")
                
            Catch ex As Exception
                Console.WriteLine($"UpdateFromProjectStructure error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Recursively process nodes from ProjectParser's syntax tree
        ''' </summary>
        ''' <param name="vNode">The node to process</param>
        Private Sub ProcessProjectNode(vNode As SyntaxNode, vSymbolTable As Dictionary(Of String, SyntaxNode))
            Try
                If vNode Is Nothing OrElse vSymbolTable Is Nothing Then Return
                
                ' Add node to symbol index if it's a meaningful symbol
                Select Case vNode.NodeType
                    Case CodeNodeType.eClass, CodeNodeType.eInterface, CodeNodeType.eStructure,
                         CodeNodeType.eEnum, CodeNodeType.eModule
                        ' Add type to project symbols
                        If Not String.IsNullOrEmpty(vNode.Name) Then
                            vSymbolTable(vNode.Name) = vNode
                        End If
                        
                    Case CodeNodeType.eMethod, CodeNodeType.eProperty, CodeNodeType.eField,
                         CodeNodeType.eEvent
                        ' Add member to project symbols with qualified name
                        Dim lQualifiedName As String = GetQualifiedNodeName(vNode)
                        If Not String.IsNullOrEmpty(lQualifiedName) Then
                            vSymbolTable(lQualifiedName) = vNode
                        End If
                End Select
                
                ' Process children recursively
                for each lChild in vNode.Children
                    ProcessProjectNode(lChild, vSymbolTable)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ProcessProjectNode error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Gets the fully qualified name of a node
        ''' </summary>
        Private Function GetQualifiedNodeName(vNode As SyntaxNode) As String
            Try
                If vNode Is Nothing Then Return ""
                
                Dim lParts As New List(Of String)()
                Dim lCurrent As SyntaxNode = vNode
                
                While lCurrent IsNot Nothing
                    If Not String.IsNullOrEmpty(lCurrent.Name) AndAlso
                       lCurrent.NodeType <> CodeNodeType.eDocument Then
                        lParts.Insert(0, lCurrent.Name)
                    End If
                    lCurrent = lCurrent.Parent
                End While
                
                Return String.Join(".", lParts)
                
            Catch ex As Exception
                Console.WriteLine($"GetQualifiedNodeName error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ''' <summary>
        ''' Add project reference assembly
        ''' </summary>
        ''' <param name="vAssemblyPath">Path to the assembly to reference</param>
        Public Sub AddReference(vAssemblyPath As String)
            Try
                Dim lAssembly As Assembly = Assembly.LoadFrom(vAssemblyPath)
                If Not pProjectReferences.Contains(lAssembly) Then
                    pProjectReferences.Add(lAssembly)
                    
                    ' Clear type cache to reload with new assembly
                    pTypeCache.Clear()
                    
                    Console.WriteLine($"CodeSenseEngine: Added reference to {lAssembly.GetName().Name}")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"AddReference error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Clear all references
        ''' </summary>
        Public Sub ClearReferences()
            Try
                pProjectReferences.Clear()
                pTypeCache.Clear()
                pMemberCache.Clear()
                Console.WriteLine("CodeSenseEngine: All references cleared")
                
            Catch ex As Exception
                Console.WriteLine($"ClearReferences error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Gets code completion suggestions for the given context
        ''' </summary>
        ''' <param name="vContext">The current code context</param>
        ''' <returns>List of code completion suggestions</returns>
        Public Function GetSuggestions(vContext As CodeSenseContext) As List(Of CodeSenseSuggestion)
            Try
                pCurrentContext = vContext
                Dim lSuggestions As New List(Of CodeSenseSuggestion)()

                ' Determine what kind of suggestions to provide
                Select Case vContext.TriggerKind
                    Case CodeSenseTriggerKind.eDot
                        lSuggestions = GetMemberSuggestions(vContext)

                    Case CodeSenseTriggerKind.eSpace
                        lSuggestions = GetContextualSuggestions(vContext)

                    Case CodeSenseTriggerKind.eOpenParen
                        lSuggestions = GetParameterHints(vContext)

                    Case CodeSenseTriggerKind.eManual
                        lSuggestions = GetGeneralSuggestions(vContext)

                    Case Else
                        lSuggestions = GetGeneralSuggestions(vContext)
                End Select

                ' Filter by whatever the user has typed so far, so the popup narrows as they type
                ' instead of always showing the full unfiltered candidate set
                Dim lFilterText As String = If(Not String.IsNullOrEmpty(vContext.CurrentWord), vContext.CurrentWord, vContext.Prefix)
                lSuggestions = FilterSuggestionsByPrefix(lSuggestions, lFilterText)

                ' Sort suggestions by relevance
                lSuggestions.Sort(AddressOf CompareSuggestions)

                Return lSuggestions

            Catch ex As Exception
                Console.WriteLine($"GetSuggestions error: {ex.Message}")
                Return New List(Of CodeSenseSuggestion)()
            End Try
        End Function

        ''' <summary>
        ''' Filters suggestions to those whose Text starts with the given filter text
        ''' </summary>
        ''' <param name="vSuggestions">Unfiltered suggestion list</param>
        ''' <param name="vFilterText">Text typed so far (case-insensitive, VB.NET identifiers are case-insensitive); empty means no filtering</param>
        ''' <returns>The filtered list, or the original list unchanged if vFilterText is empty</returns>
        Private Function FilterSuggestionsByPrefix(vSuggestions As List(Of CodeSenseSuggestion), vFilterText As String) As List(Of CodeSenseSuggestion)
            Try
                If String.IsNullOrEmpty(vFilterText) OrElse vSuggestions Is Nothing Then Return vSuggestions

                Dim lFiltered As New List(Of CodeSenseSuggestion)()
                for each lSuggestion in vSuggestions
                    If lSuggestion.Text IsNot Nothing AndAlso lSuggestion.Text.StartsWith(vFilterText, StringComparison.OrdinalIgnoreCase) Then
                        lFiltered.Add(lSuggestion)
                    End If
                Next

                Return lFiltered

            Catch ex As Exception
                Console.WriteLine($"FilterSuggestionsByPrefix error: {ex.Message}")
                Return vSuggestions
            End Try
        End Function
        
        ' ===== Private Methods - Initialization =====
        
        ''' <summary>
        ''' Gets the ProjectManager instance via event
        ''' </summary>
        ''' <returns>The ProjectManager if available, Nothing otherwise</returns>
        Private Function GetProjectManager() As Managers.ProjectManager
            Try
                Dim lEventArgs As New ProjectManagerRequestEventArgs()
                RaiseEvent ProjectManagerRequested(Me, lEventArgs)
                
                If lEventArgs.HasProjectManager Then
                    Return lEventArgs.ProjectManager
                End If
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"GetProjectManager error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Subscribe to ProjectManager parse events
        ''' </summary>
        Private Sub SubscribeToProjectParseEvents()
            Try
                Dim lProjectManager = GetProjectManager()
                If lProjectManager IsNot Nothing Then
                    ' Subscribe to ParseCompleted event
                    RemoveHandler lProjectManager.ParseCompleted, AddressOf OnProjectParseCompleted
                    AddHandler lProjectManager.ParseCompleted, AddressOf OnProjectParseCompleted
                    
                    ' Subscribe to ProjectStructureLoaded event
                    RemoveHandler lProjectManager.ProjectStructureLoaded, AddressOf OnProjectStructureLoaded
                    AddHandler lProjectManager.ProjectStructureLoaded, AddressOf OnProjectStructureLoaded
                    
                    Console.WriteLine("CodeSenseEngine subscribed to ProjectManager parse events")
                Else
                    Console.WriteLine("CodeSenseEngine: No ProjectManager available to subscribe to")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SubscribeToProjectParseEvents error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Handle project structure loaded from ProjectManager
        ''' </summary>
        Private Sub OnProjectStructureLoaded(vRootNode As SyntaxNode)
            Try
                ' Update with the full project tree
                UpdateFromSyntaxTree(vRootNode, True)
                
            Catch ex As Exception
                Console.WriteLine($"OnProjectStructureLoaded error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handle parse completion from ProjectManager
        ''' </summary>
        Private Sub OnProjectParseCompleted(vFile As SourceFileInfo, vResult As Object)
            Try
                ' Update from the parse result
                UpdateFromParseResult(vFile, vResult)
                
            Catch ex As Exception
                Console.WriteLine($"OnProjectParseCompleted error: {ex.Message}")
            End Try
        End Sub
        

        
        ''' <summary>
        ''' Initialize keyword suggestions
        ''' </summary>
        Private Sub InitializeKeywordSuggestions()
            Try
                pKeywordSuggestions = New List(Of CodeSenseSuggestion) From {
                    New CodeSenseSuggestion() with {.Text = "Public", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "Private", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "Protected", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "Friend", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "Shared", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "Class", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "Module", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "Interface", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "Function", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "Sub", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "Property", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "If", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "Then", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "Else", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "ElseIf", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "End If", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "For", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "Next", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "While", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "End While", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "Try", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "Catch", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "Finally", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "End Try", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "Dim", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "As", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "New", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "Return", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "Nothing", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "True", .Kind = CodeSenseSuggestionKind.eKeyword},
                    New CodeSenseSuggestion() with {.Text = "False", .Kind = CodeSenseSuggestionKind.eKeyword}
                }
                
            Catch ex As Exception
                Console.WriteLine($"InitializeKeywordSuggestions error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Load core .NET assemblies for type information
        ''' </summary>
        Private Sub LoadCoreAssemblies()
            Try
                ' Add basic assemblies
                pProjectReferences.Add(GetType(Object).Assembly) ' mscorlib
                pProjectReferences.Add(GetType(System.Collections.Generic.List(Of )).Assembly) ' System.Core
                pProjectReferences.Add(GetType(Microsoft.VisualBasic.Collection).Assembly) ' Microsoft.VisualBasic
                
            Catch ex As Exception
                Console.WriteLine($"LoadCoreAssemblies error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Private Methods - Suggestions =====
        
        ''' <summary>
        ''' Get member suggestions after a dot
        ''' </summary>
        Private Function GetMemberSuggestions(vContext As CodeSenseContext) As List(Of CodeSenseSuggestion)
            Try
                Dim lSuggestions As New List(Of CodeSenseSuggestion)()
                
                ' Get the identifier before the dot
                Dim lTarget As String = vContext.MemberAccessTarget
                If String.IsNullOrEmpty(lTarget) Then
                    Return lSuggestions
                End If
                
                Dim lNode As SyntaxNode = Nothing
                
                ' Handle "Me" keyword
                If lTarget.Equals("Me", StringComparison.OrdinalIgnoreCase) Then
                    ' Find the containing class from the context
                    If pCurrentSyntaxTree IsNot Nothing Then
                        lNode = FindContainingClass(pCurrentSyntaxTree, vContext.TriggerPosition.Line)
                    End If
                Else
                    ' Try to find the node in our syntax trees
                    lNode = FindNodeByName(lTarget)
                End If
                
                If lNode IsNot Nothing Then
                    ' Add members from the node
                    AddNodeMemberSuggestions(lNode, lSuggestions)
                End If
                
                ' Also check for types in referenced assemblies (only if not Me for now)
                If Not lTarget.Equals("Me", StringComparison.OrdinalIgnoreCase) Then
                     AddTypeMemberSuggestions(lTarget, lSuggestions)
                End If
                
                Return lSuggestions
                
            Catch ex As Exception
                Console.WriteLine($"GetMemberSuggestions error: {ex.Message}")
                Return New List(Of CodeSenseSuggestion)()
            End Try
        End Function
        
        ''' <summary>
        ''' Get contextual suggestions based on current line
        ''' </summary>
        Private Function GetContextualSuggestions(vContext As CodeSenseContext) As List(Of CodeSenseSuggestion)
            Try
                Dim lSuggestions As New List(Of CodeSenseSuggestion)()
                
                ' Add keyword suggestions
                lSuggestions.AddRange(pKeywordSuggestions)
                
                ' Add types from current scope
                AddScopeTypeSuggestions(vContext, lSuggestions)
                
                ' Add local variables and parameters
                AddLocalSuggestions(vContext, lSuggestions)
                
                Return lSuggestions
                
            Catch ex As Exception
                Console.WriteLine($"GetContextualSuggestions error: {ex.Message}")
                Return New List(Of CodeSenseSuggestion)()
            End Try
        End Function
        
        ''' <summary>
        ''' Get parameter hints after opening parenthesis
        ''' </summary>
        Private Function GetParameterHints(vContext As CodeSenseContext) As List(Of CodeSenseSuggestion)
            Try
                ' TODO: Implement parameter hints based on method signatures
                Return New List(Of CodeSenseSuggestion)()
                
            Catch ex As Exception
                Console.WriteLine($"GetParameterHints error: {ex.Message}")
                Return New List(Of CodeSenseSuggestion)()
            End Try
        End Function
        
        ''' <summary>
        ''' Get general suggestions for manual trigger
        ''' </summary>
        Private Function GetGeneralSuggestions(vContext As CodeSenseContext) As List(Of CodeSenseSuggestion)
            Try
                Dim lSuggestions As New List(Of CodeSenseSuggestion)()
                
                ' Add keywords
                lSuggestions.AddRange(pKeywordSuggestions)
                
                ' Add all types in scope
                AddScopeTypeSuggestions(vContext, lSuggestions)
                
                ' Add local variables and parameters
                AddLocalSuggestions(vContext, lSuggestions)
                
                ' Add project types
                AddProjectTypeSuggestions(lSuggestions)
                
                Return lSuggestions
                
            Catch ex As Exception
                Console.WriteLine($"GetGeneralSuggestions error: {ex.Message}")
                Return New List(Of CodeSenseSuggestion)()
            End Try
        End Function
        
        ''' <summary>
        ''' Add suggestions from a SyntaxNode's members
        ''' </summary>
        Private Sub AddNodeMemberSuggestions(vNode As SyntaxNode, vSuggestions As List(Of CodeSenseSuggestion))
            Try
                If vNode Is Nothing OrElse vNode.Children Is Nothing Then Return
                
                for each lChild in vNode.Children
                    Select Case lChild.NodeType
                        Case CodeNodeType.eMethod, CodeNodeType.eFunction
                            Dim lSuggestion As New CodeSenseSuggestion()
                            lSuggestion.Text = lChild.Name
                            lSuggestion.Kind = CodeSenseSuggestionKind.eMethod
                            lSuggestion.Icon = "method"
                            lSuggestion.Description = GetDescriptionForNode(lChild)
                            
                            ' Check visibility
                            If IsNodeAccessible(lChild) Then
                                vSuggestions.Add(lSuggestion)
                            End If
                            
                        Case CodeNodeType.eProperty
                            Dim lSuggestion As New CodeSenseSuggestion()
                            lSuggestion.Text = lChild.Name
                            lSuggestion.Kind = CodeSenseSuggestionKind.eProperty
                            lSuggestion.Icon = "property"
                            lSuggestion.Description = GetDescriptionForNode(lChild)
                            
                            If IsNodeAccessible(lChild) Then
                                vSuggestions.Add(lSuggestion)
                            End If
                            
                        Case CodeNodeType.eField
                            Dim lSuggestion As New CodeSenseSuggestion()
                            lSuggestion.Text = lChild.Name
                            lSuggestion.Kind = CodeSenseSuggestionKind.eField
                            lSuggestion.Icon = "field"
                            lSuggestion.Description = GetDescriptionForNode(lChild)
                            
                            If IsNodeAccessible(lChild) Then
                                vSuggestions.Add(lSuggestion)
                            End If
                    End Select
                Next
                
            Catch ex As Exception
                Console.WriteLine($"AddNodeMemberSuggestions error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Add type member suggestions from referenced assemblies
        ''' </summary>
        Private Sub AddTypeMemberSuggestions(vTypeName As String, vSuggestions As List(Of CodeSenseSuggestion))
            Try
                ' Iterate through referenced assemblies
                If pProjectReferences Is Nothing Then Return
                
                For Each lAssembly As Assembly In pProjectReferences
                    ' Check 1: Exact Type match
                    Dim lType As Type = lAssembly.GetType(vTypeName, False, True)
                    
                    ' Check 2: Try checking for System.TypeName if not found (e.g. Console -> System.Console)
                    If lType Is Nothing AndAlso Not vTypeName.Contains(".") Then
                        lType = lAssembly.GetType("System." & vTypeName, False, True)
                    End If
                    
                     ' Check 3: Check against Imports
                    If lType Is Nothing AndAlso pCurrentContext?.ImportsContext IsNot Nothing Then
                        For Each lImport As String In pCurrentContext.ImportsContext
                            lType = lAssembly.GetType(lImport & "." & vTypeName, False, True)
                            If lType IsNot Nothing Then Exit For
                        Next
                    End If
                    
                    If lType IsNot Nothing Then
                        ' Found the type, add its members
                        AddMemberSuggestionsFromType(lType, vSuggestions)
                        Exit For
                    Else
                        ' Check 4: Check if vTypeName is a Namespace
                        ' Since Namespaces aren't Types, we have to scan exported types to see if they belong to this namespace
                        Dim lFoundNamespace As Boolean = False
                        Dim lNamespacePrefix As String = vTypeName & "."
                        
                        ' Also check if vTypeName is a child namespace of an Import
                        Dim lNamespaceOptions As New List(Of String)
                        lNamespaceOptions.Add(vTypeName)
                         If pCurrentContext?.ImportsContext IsNot Nothing Then
                            For Each lImport As String In pCurrentContext.ImportsContext
                                lNamespaceOptions.Add(lImport & "." & vTypeName)
                            Next
                        End If
                        
                        For Each lExportedType As Type In lAssembly.GetExportedTypes()
                            For Each lNamespaceCheck As String In lNamespaceOptions
                                If lExportedType.Namespace IsNot Nothing AndAlso 
                                   (lExportedType.Namespace.Equals(lNamespaceCheck, StringComparison.OrdinalIgnoreCase) OrElse 
                                    lExportedType.Namespace.StartsWith(lNamespaceCheck & ".", StringComparison.OrdinalIgnoreCase)) Then
                                    
                                    ' Found a match!
                                    ' If exact namespace match, add Type
                                    If lExportedType.Namespace.Equals(lNamespaceCheck, StringComparison.OrdinalIgnoreCase) Then
                                         Dim lSuggestion As New CodeSenseSuggestion()
                                        lSuggestion.Text = lExportedType.Name
                                        lSuggestion.Kind = If(lExportedType.IsInterface, CodeSenseSuggestionKind.eInterface, 
                                                              If(lExportedType.IsEnum, CodeSenseSuggestionKind.eSnippet, CodeSenseSuggestionKind.eClass))
                                        lSuggestion.Icon = If(lExportedType.IsInterface, "interface", 
                                                              If(lExportedType.IsEnum, "enum", "class"))
                                        lSuggestion.Description = lExportedType.FullName
                                        
                                        ' Add unique
                                        If Not vSuggestions.Any(Function(s) s.Text = lSuggestion.Text) Then
                                            vSuggestions.Add(lSuggestion)
                                        End If
                                    
                                    Else
                                        ' Child namespace
                                        ' Extract the immediate child name
                                        ' e.g. looking for "System", found "System.Collections.Generic.List"
                                        ' We want "Collections"
                                        
                                        Dim lRemaining As String = lExportedType.Namespace.Substring(lNamespaceCheck.Length + 1)
                                        Dim lLimit As Integer = lRemaining.IndexOf(".")
                                        Dim lChildNs As String = If(lLimit > 0, lRemaining.Substring(0, lLimit), lRemaining)
                                        
                                        Dim lSuggestion As New CodeSenseSuggestion()
                                        lSuggestion.Text = lChildNs
                                        lSuggestion.Kind = CodeSenseSuggestionKind.eNamespace
                                        lSuggestion.Icon = "namespace"
                                        lSuggestion.Description = "Namespace " & lChildNs
                                        
                                        If Not vSuggestions.Any(Function(s) s.Text = lSuggestion.Text) Then
                                            vSuggestions.Add(lSuggestion)
                                        End If
                                    End If
                                    
                                    lFoundNamespace = True
                                End If
                            Next
                        Next
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"AddTypeMemberSuggestions error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub AddMemberSuggestionsFromType(vType As Type, vSuggestions As List(Of CodeSenseSuggestion))
            For Each lMember As MemberInfo In vType.GetMembers(BindingFlags.Public Or BindingFlags.Instance Or BindingFlags.Static)
                
                ' Filter out "special" names (internal getters/setters/constructors usually)
                If lMember.Name.StartsWith("get_") OrElse lMember.Name.StartsWith("set_") OrElse lMember.Name.StartsWith(".ctor") Then
                    Continue For
                End If
                
                Dim lSuggestion As New CodeSenseSuggestion()
                lSuggestion.Text = lMember.Name
                
                Select Case lMember.MemberType
                    Case MemberTypes.Method
                        lSuggestion.Kind = CodeSenseSuggestionKind.eMethod
                        lSuggestion.Icon = "method"
                        lSuggestion.Description = "Method " & lMember.Name
                        
                    Case MemberTypes.Property
                        lSuggestion.Kind = CodeSenseSuggestionKind.eProperty
                        lSuggestion.Icon = "property"
                        lSuggestion.Description = "Property " & lMember.Name
                        
                    Case MemberTypes.Field
                        lSuggestion.Kind = CodeSenseSuggestionKind.eField
                        lSuggestion.Icon = "field"
                        lSuggestion.Description = "Field " & lMember.Name
                        
                    Case MemberTypes.Event
                        lSuggestion.Kind = CodeSenseSuggestionKind.eEvent
                        lSuggestion.Icon = "event"
                        lSuggestion.Description = "Event " & lMember.Name
                End Select
                
                ' Avoid duplicates
                Dim lFound As Boolean = False
                For Each lExist In vSuggestions
                    If lExist.Text = lSuggestion.Text Then
                        lFound = True
                        Exit For
                    End If
                Next
                
                If Not lFound AndAlso lSuggestion.Icon <> "" Then
                    vSuggestions.Add(lSuggestion)
                End If
            Next
        End Sub
        
        ''' <summary>
        ''' Parse Imports statements from text
        ''' </summary>
        Public Function ParseImports(vText As String) As List(Of String)
            Dim lImports As New List(Of String)()
            Try
                Dim lLines = vText.Split(New Char() {vbLf, vbCr}, StringSplitOptions.RemoveEmptyEntries)
                
                ' Only check first 50 lines for optimization
                Dim lLimit As Integer = Math.Min(lLines.Length - 1, 50)
                
                For i As Integer = 0 To lLimit
                    Dim lLine = lLines(i).Trim()
                    If lLine.StartsWith("Imports ", StringComparison.OrdinalIgnoreCase) Then
                        Dim lNs = lLine.Substring(8).Trim()
                        If Not String.IsNullOrEmpty(lNs) Then
                            lImports.Add(lNs)
                        End If
                    ElseIf Not lLine.StartsWith("'") AndAlso Not String.IsNullOrEmpty(lLine) AndAlso Not lLine.StartsWith("Option ") Then
                         ' Stop at first real code line (Class, Module, Namespace, etc.)
                         ' But be lenient with comments and Option statements
                         If lLine.StartsWith("Class ") OrElse lLine.StartsWith("Module ") OrElse lLine.StartsWith("Namespace ") OrElse lLine.StartsWith("Public ") Then
                            ' Usually imports are at top, but can be inside namespaces. 
                            ' For now, just top level.
                             Exit For
                         End If
                    End If
                Next
            Catch ex As Exception
                Console.WriteLine($"ParseImports error: {ex.Message}")
            End Try
            Return lImports
        End Function
        

        
        ''' <summary>
        ''' Add types available in current scope
        ''' </summary>
        Private Sub AddScopeTypeSuggestions(vContext As CodeSenseContext, vSuggestions As List(Of CodeSenseSuggestion))
            Try
                ' TODO: Add types from current namespace and imports
                
            Catch ex As Exception
                Console.WriteLine($"AddScopeTypeSuggestions error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Add local variables and parameters
        ''' </summary>
        Private Sub AddLocalSuggestions(vContext As CodeSenseContext, vSuggestions As List(Of CodeSenseSuggestion))
            Try
                If vContext.LocalVariables IsNot Nothing Then
                    for each lVar in vContext.LocalVariables
                        Dim lSuggestion As New CodeSenseSuggestion()
                        lSuggestion.Text = lVar
                        lSuggestion.Kind = CodeSenseSuggestionKind.eLocalVariable
                        lSuggestion.Icon = "variable"
                        vSuggestions.Add(lSuggestion)
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"AddLocalSuggestions error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Add types from the project syntax tree
        ''' </summary>
        Private Sub AddProjectTypeSuggestions(vSuggestions As List(Of CodeSenseSuggestion))
            Try
                If pProjectSyntaxTree Is Nothing Then Return
                
                ' Walk the tree looking for types
                AddProjectTypesRecursive(pProjectSyntaxTree, vSuggestions)
                
            Catch ex As Exception
                Console.WriteLine($"AddProjectTypeSuggestions error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Recursively add project types
        ''' </summary>
        Private Sub AddProjectTypesRecursive(vNode As SyntaxNode, vSuggestions As List(Of CodeSenseSuggestion))
            Try
                If vNode Is Nothing Then Return
                
                Select Case vNode.NodeType
                    Case CodeNodeType.eClass
                        Dim lSuggestion As New CodeSenseSuggestion()
                        lSuggestion.Text = vNode.Name
                        lSuggestion.Kind = CodeSenseSuggestionKind.eClass
                        lSuggestion.Icon = "class"
                        lSuggestion.Description = $"Class {vNode.Name}"
                        vSuggestions.Add(lSuggestion)
                        
                    Case CodeNodeType.eInterface
                        Dim lSuggestion As New CodeSenseSuggestion()
                        lSuggestion.Text = vNode.Name
                        lSuggestion.Kind = CodeSenseSuggestionKind.eInterface
                        lSuggestion.Icon = "interface"
                        lSuggestion.Description = $"Interface {vNode.Name}"
                        vSuggestions.Add(lSuggestion)
                End Select
                
                ' Recursively process children
                If vNode.Children IsNot Nothing Then
                    for each lChild in vNode.Children
                        AddProjectTypesRecursive(lChild, vSuggestions)
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"AddProjectTypesRecursive error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Helper Methods =====
        
        ''' <summary>
        ''' Count nodes in a syntax tree
        ''' </summary>
        Private Function CountNodes(vNode As SyntaxNode) As Integer
            Try
                If vNode Is Nothing Then Return 0
                
                Dim lCount As Integer = 1
                If vNode.Children IsNot Nothing Then
                    for each lChild in vNode.Children
                        lCount += CountNodes(lChild)
                    Next
                End If
                
                Return lCount
                
            Catch ex As Exception
                Console.WriteLine($"CountNodes error: {ex.Message}")
                Return 0
            End Try
        End Function
        
        ''' <summary>
        ''' Find a node by name in the syntax trees
        ''' </summary>
        Private Function FindNodeByName(vName As String) As SyntaxNode
            Try
                ' Search current document first
                Dim lNode As SyntaxNode = Nothing
                If pCurrentSyntaxTree IsNot Nothing Then
                    lNode = FindNodeByNameRecursive(pCurrentSyntaxTree, vName)
                End If
                
                ' If not found, search project tree
                If lNode Is Nothing AndAlso pProjectSyntaxTree IsNot Nothing Then
                    lNode = FindNodeByNameRecursive(pProjectSyntaxTree, vName)
                End If
                
                Return lNode
                
            Catch ex As Exception
                Console.WriteLine($"FindNodeByName error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Find the class containing the specified line
        ''' </summary>
        Private Function FindContainingClass(vNode As SyntaxNode, vLine As Integer) As SyntaxNode
            Try
                If vNode Is Nothing Then Return Nothing
                
                ' Check if this node is a class and contains the line
                If vNode.NodeType = CodeNodeType.eClass OrElse vNode.NodeType = CodeNodeType.eModule Then
                    ' Check line range if available
                    If vNode.StartLine <= vLine AndAlso vNode.EndLine >= vLine Then
                        ' This class/module contains the line. 
                        ' However, there might be a nested class that is a better match.
                        ' So we continue searching children.
                        
                        Dim lBetterMatch As SyntaxNode = Nothing
                        If vNode.Children IsNot Nothing Then
                            for each lChild in vNode.Children
                                Dim lResult = FindContainingClass(lChild, vLine)
                                If lResult IsNot Nothing Then
                                    lBetterMatch = lResult
                                    Exit For
                                End If
                            Next
                        End If
                        
                        If lBetterMatch IsNot Nothing Then
                            Return lBetterMatch
                        Else
                            Return vNode
                        End If
                    End If
                End If
                
                ' If not a match or not a container, check children
                If vNode.Children IsNot Nothing Then
                    for each lChild in vNode.Children
                        Dim lResult = FindContainingClass(lChild, vLine)
                        If lResult IsNot Nothing Then
                            Return lResult
                        End If
                    Next
                End If
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"FindContainingClass error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Recursively find a node by name
        ''' </summary>
        Private Function FindNodeByNameRecursive(vNode As SyntaxNode, vName As String) As SyntaxNode
            Try
                If vNode Is Nothing Then Return Nothing
                
                If String.Equals(vNode.Name, vName, StringComparison.OrdinalIgnoreCase) Then
                    Return vNode
                End If
                
                If vNode.Children IsNot Nothing Then
                    for each lChild in vNode.Children
                        Dim lFound As SyntaxNode = FindNodeByNameRecursive(lChild, vName)
                        If lFound IsNot Nothing Then
                            Return lFound
                        End If
                    Next
                End If
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"FindNodeByNameRecursive error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Compare suggestions for sorting
        ''' </summary>
        Private Function CompareSuggestions(vX As CodeSenseSuggestion, vY As CodeSenseSuggestion) As Integer
            Try
                ' Keywords come first
                If vX.Kind = CodeSenseSuggestionKind.eKeyword AndAlso vY.Kind <> CodeSenseSuggestionKind.eKeyword Then
                    Return -1
                ElseIf vY.Kind = CodeSenseSuggestionKind.eKeyword AndAlso vX.Kind <> CodeSenseSuggestionKind.eKeyword Then
                    Return 1
                End If
                
                ' Then alphabetical
                Return String.Compare(vX.Text, vY.Text, StringComparison.OrdinalIgnoreCase)
                
            Catch ex As Exception
                Console.WriteLine($"CompareSuggestions error: {ex.Message}")
                Return 0
            End Try
        End Function
        
        ''' <summary>
        ''' Get icon name for node type
        ''' </summary>
        Private Function GetIconForNodeType(vNodeType As CodeNodeType) As String
            Select Case vNodeType
                Case CodeNodeType.eClass : Return "class"
                Case CodeNodeType.eInterface : Return "interface"
                Case CodeNodeType.eModule : Return "module"
                Case CodeNodeType.eStructure : Return "structure"
                Case CodeNodeType.eEnum : Return "enum"
                Case CodeNodeType.eMethod, CodeNodeType.eFunction : Return "method"
                Case CodeNodeType.eProperty : Return "property"
                Case CodeNodeType.eField : Return "field"
                Case CodeNodeType.eEvent : Return "event"
                Case CodeNodeType.eNamespace : Return "namespace"
                Case Else : Return "default"
            End Select
        End Function
        
        ''' <summary>
        ''' Get description for a node
        ''' </summary>
        Private Function GetDescriptionForNode(vNode As SyntaxNode) As String
            Try
                Dim lDescription As String = ""
                
                Select Case vNode.NodeType
                    Case CodeNodeType.eClass
                        lDescription = $"Class {vNode.Name}"
                    Case CodeNodeType.eModule
                        lDescription = $"Module {vNode.Name}"
                    Case CodeNodeType.eInterface
                        lDescription = $"Interface {vNode.Name}"
                    Case CodeNodeType.eMethod
                        lDescription = $"Sub {vNode.Name}"
                    Case CodeNodeType.eFunction
                        lDescription = $"Function {vNode.Name}"
                    Case CodeNodeType.eProperty
                        lDescription = $"Property {vNode.Name}"
                    Case CodeNodeType.eField
                        lDescription = $"Field {vNode.Name}"
                    Case CodeNodeType.eEvent
                        lDescription = $"Event {vNode.Name}"
                    Case Else
                        lDescription = vNode.Name
                End Select
                
                ' Add XML documentation if available
                If vNode.XmlDocumentation IsNot Nothing Then
                    lDescription &= Environment.NewLine & vNode.XmlDocumentation.Summary
                End If
                
                Return lDescription
                
            Catch ex As Exception
                Console.WriteLine($"GetDescriptionForNode error: {ex.Message}")
                Return vNode.Name
            End Try
        End Function
        
        ''' <summary>
        ''' Check if a node is accessible in current context
        ''' </summary>
        Private Function IsNodeAccessible(vNode As SyntaxNode) As Boolean
            Try
                ' For now, return true for all public and friend members
                ' TODO: Implement proper visibility checking based on context
                Return vNode.IsPublic OrElse vNode.IsFriend
                
            Catch ex As Exception
                Console.WriteLine($"IsNodeAccessible error: {ex.Message}")
                Return True
            End Try
        End Function
        
        ' ===== IDisposable Implementation =====
        
        ''' <summary>
        ''' Dispose of resources
        ''' </summary>
        Public Sub Dispose() Implements IDisposable.Dispose
            Try
                If Not pDisposed Then
                    ' Unsubscribe from events
                    Dim lProjectManager = GetProjectManager()
                    If lProjectManager IsNot Nothing Then
                        RemoveHandler lProjectManager.ParseCompleted, AddressOf OnProjectParseCompleted
                        RemoveHandler lProjectManager.ProjectStructureLoaded, AddressOf OnProjectStructureLoaded
                    End If
                    
                    ' Clear collections
                    pProjectReferences?.Clear()
                    pTypeCache?.Clear()
                    pMemberCache?.Clear()
                    pKeywordSuggestions?.Clear()
                    
                    pDisposed = True
                End If
                
            Catch ex As Exception
                Console.WriteLine($"Dispose error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
