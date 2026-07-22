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
        Private pTopLevelNamespaceCache As List(Of String) = Nothing
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
                Return IsNodeAccessible(vNode)

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
                pTopLevelNamespaceCache = Nothing

                ' Re-seed the core framework assemblies immediately so callers that clear
                ' references before adding project-specific ones (e.g. MainWindow.
                ' UpdateCodeSenseReferences) never leave the engine without System/etc.
                LoadCoreAssemblies()

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

                ' Populate Imports/containing-class/containing-method/locals from the live
                ' syntax tree before dispatching - every trigger kind below reads these
                PopulateScopeContext(vContext)

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

                    If lNode IsNot Nothing Then
                        AddNodeMemberSuggestions(lNode, lSuggestions)
                    End If

                    Return lSuggestions
                End If

                ' Resolve the DECLARED TYPE of the identifier (parameter, local, field, or
                ' property visible at the cursor) first, rather than treating the identifier
                ' text itself as a type name - this is what makes "lFoo." resolve based on
                ' what lFoo actually is, not just when lFoo happens to share a name with a type
                Dim lResolvedType As String = ResolveVariableType(lTarget, vContext)
                If Not String.IsNullOrEmpty(lResolvedType) AndAlso Not lResolvedType.Equals("Object", StringComparison.OrdinalIgnoreCase) Then
                    Dim lTypeNode As SyntaxNode = FindTypeNodeByName(lResolvedType)
                    If lTypeNode IsNot Nothing Then
                        AddNodeMemberSuggestions(lTypeNode, lSuggestions)
                    End If

                    ' Also try framework/reflection lookup using the resolved type name
                    ' (handles e.g. "lFoo As String" -> System.String members)
                    AddTypeMemberSuggestions(lResolvedType, lSuggestions)

                    Return lSuggestions
                End If

                ' Fallback: previous behavior - treat the identifier text itself as a type/class
                ' name (handles "Console.", "ThemeManager.", etc., and Option Infer'd locals
                ' whose declared type couldn't be resolved above)
                lNode = FindNodeByName(lTarget)

                If lNode IsNot Nothing Then
                    ' Add members from the node
                    AddNodeMemberSuggestions(lNode, lSuggestions)
                End If

                AddTypeMemberSuggestions(lTarget, lSuggestions)

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
                Dim lSuggestions As New List(Of CodeSenseSuggestion)()

                ' MemberAccessTarget carries the identifier before the "(" for this trigger kind
                ' (set by CustomDrawingEditor.GetIdentifierBeforeParen)
                Dim lTarget As String = vContext.MemberAccessTarget
                If String.IsNullOrEmpty(lTarget) Then Return lSuggestions

                ' Best-effort lookup by name - does not disambiguate overloads, matching the
                ' same single-node-by-name approach GetMemberSuggestions/FindNodeByName uses
                Dim lNode As SyntaxNode = FindNodeByName(lTarget)
                If lNode Is Nothing OrElse lNode.Parameters Is Nothing OrElse lNode.Parameters.Count = 0 Then
                    Return lSuggestions
                End If

                Dim lParamParts As New List(Of String)()
                for each lParam in lNode.Parameters
                    Dim lPart As String = $"{lParam.Name} As {lParam.ParameterType}"
                    If lParam.IsOptional Then lPart = $"[{lPart}]"
                    lParamParts.Add(lPart)
                Next

                Dim lSuggestion As New CodeSenseSuggestion()
                lSuggestion.Text = lNode.Name
                lSuggestion.Kind = CodeSenseSuggestionKind.eMethod
                lSuggestion.Icon = "method"
                lSuggestion.Signature = $"{lNode.Name}({String.Join(", ", lParamParts)})"
                lSuggestion.Description = lSuggestion.Signature
                lSuggestions.Add(lSuggestion)

                Return lSuggestions

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

                ' Add all types in scope (AddScopeTypeSuggestions delegates to AddProjectTypeSuggestions,
                ' so this alone covers project types - a separate direct call here would duplicate them)
                AddScopeTypeSuggestions(vContext, lSuggestions)

                ' Add local variables and parameters
                AddLocalSuggestions(vContext, lSuggestions)

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

                If vNode.NodeType = CodeNodeType.eNamespace Then
                    ' A namespace's members are types and nested namespaces, not
                    ' methods/properties/fields - handle it separately
                    Dim lSeenNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                    AddNamespaceMemberSuggestions(vNode, vSuggestions, lSeenNames)
                    Return
                End If

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
        ''' Adds a namespace's direct child types and nested namespaces as suggestions
        ''' </summary>
        ''' <param name="vNamespaceNode">The namespace node (e.g. the project's synthesized root namespace)</param>
        ''' <param name="vSuggestions">Suggestion list to append to</param>
        ''' <param name="vSeenNames">Names already added - a namespace's children include one
        ''' node per partial-class declaring file, so this prevents e.g. "MainWindow" from
        ''' appearing once per MainWindow.*.vb file</param>
        Private Sub AddNamespaceMemberSuggestions(vNamespaceNode As SyntaxNode, vSuggestions As List(Of CodeSenseSuggestion), vSeenNames As HashSet(Of String))
            Try
                If vNamespaceNode.Children Is Nothing Then Return

                for each lChild in vNamespaceNode.Children
                    If Not IsNodeAccessible(lChild) Then Continue for

                    Select Case lChild.NodeType
                        Case CodeNodeType.eClass
                            If vSeenNames.Add(lChild.Name) Then
                                Dim lSuggestion As New CodeSenseSuggestion()
                                lSuggestion.Text = lChild.Name
                                lSuggestion.Kind = CodeSenseSuggestionKind.eClass
                                lSuggestion.Icon = "class"
                                lSuggestion.Description = $"Class {lChild.Name}"
                                vSuggestions.Add(lSuggestion)
                            End If

                        Case CodeNodeType.eInterface
                            If vSeenNames.Add(lChild.Name) Then
                                Dim lSuggestion As New CodeSenseSuggestion()
                                lSuggestion.Text = lChild.Name
                                lSuggestion.Kind = CodeSenseSuggestionKind.eInterface
                                lSuggestion.Icon = "interface"
                                lSuggestion.Description = $"Interface {lChild.Name}"
                                vSuggestions.Add(lSuggestion)
                            End If

                        Case CodeNodeType.eModule, CodeNodeType.eStructure
                            ' CodeSenseSuggestionKind has no distinct Module/Structure kind -
                            ' reuse eClass, same as the reflection-based framework-type path
                            If vSeenNames.Add(lChild.Name) Then
                                Dim lSuggestion As New CodeSenseSuggestion()
                                lSuggestion.Text = lChild.Name
                                lSuggestion.Kind = CodeSenseSuggestionKind.eClass
                                Dim lIsModule As Boolean = (lChild.NodeType = CodeNodeType.eModule)
                                lSuggestion.Icon = If(lIsModule, "module", "structure")
                                lSuggestion.Description = If(lIsModule, "Module ", "Structure ") & lChild.Name
                                vSuggestions.Add(lSuggestion)
                            End If

                        Case CodeNodeType.eEnum
                            ' Matches AddTypeMemberSuggestions's existing enum -> eSnippet convention
                            If vSeenNames.Add(lChild.Name) Then
                                Dim lSuggestion As New CodeSenseSuggestion()
                                lSuggestion.Text = lChild.Name
                                lSuggestion.Kind = CodeSenseSuggestionKind.eSnippet
                                lSuggestion.Icon = "enum"
                                lSuggestion.Description = $"Enum {lChild.Name}"
                                vSuggestions.Add(lSuggestion)
                            End If

                        Case CodeNodeType.eNamespace
                            If vSeenNames.Add(lChild.Name) Then
                                Dim lSuggestion As New CodeSenseSuggestion()
                                lSuggestion.Text = lChild.Name
                                lSuggestion.Kind = CodeSenseSuggestionKind.eNamespace
                                lSuggestion.Icon = "namespace"
                                lSuggestion.Description = $"Namespace {lChild.Name}"
                                vSuggestions.Add(lSuggestion)
                            End If
                    End Select
                Next

            Catch ex As Exception
                Console.WriteLine($"AddNamespaceMemberSuggestions error: {ex.Message}")
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
                                        lSuggestion.Text = StripGenericArity(lExportedType.Name)
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

                ' Strip the CLR generic-arity suffix (e.g. "Cast`1" -> "Cast") - VB.NET never
                ' shows the backtick-mangled reflection name, only "Cast(Of T)" style syntax
                Dim lName As String = StripGenericArity(lMember.Name)

                Dim lSuggestion As New CodeSenseSuggestion()
                lSuggestion.Text = lName

                Select Case lMember.MemberType
                    Case MemberTypes.Method
                        lSuggestion.Kind = CodeSenseSuggestionKind.eMethod
                        lSuggestion.Icon = "method"
                        lSuggestion.Description = "Method " & lName

                    Case MemberTypes.Property
                        lSuggestion.Kind = CodeSenseSuggestionKind.eProperty
                        lSuggestion.Icon = "property"
                        lSuggestion.Description = "Property " & lName

                    Case MemberTypes.Field
                        lSuggestion.Kind = CodeSenseSuggestionKind.eField
                        lSuggestion.Icon = "field"
                        lSuggestion.Description = "Field " & lName

                    Case MemberTypes.Event
                        lSuggestion.Kind = CodeSenseSuggestionKind.eEvent
                        lSuggestion.Icon = "event"
                        lSuggestion.Description = "Event " & lName
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
        ''' Strips the CLR generic-arity suffix (e.g. "List`1", "Dictionary`2") from a
        ''' reflection-derived type or member name
        ''' </summary>
        ''' <param name="vName">Raw reflection Name/MemberInfo.Name</param>
        ''' <returns>The name with everything from the backtick onward removed, unchanged if there's no backtick</returns>
        ''' <remarks>
        ''' VB.NET generics use "List(Of T)" syntax and never show the CLR's backtick-mangled
        ''' name, so this always applies regardless of the type's actual arity
        ''' </remarks>
        Private Function StripGenericArity(vName As String) As String
            If String.IsNullOrEmpty(vName) Then Return vName
            Dim lIndex As Integer = vName.IndexOf("`"c)
            Return If(lIndex >= 0, vName.Substring(0, lIndex), vName)
        End Function

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
                ' This trigger fires after keywords like As/New/Inherits/Implements/Imports where
                ' a type/namespace name is expected next - surface the project's own types and
                ' top-level framework namespaces (System, Microsoft, etc.) as candidates
                AddProjectTypeSuggestions(vSuggestions)
                AddFrameworkNamespaceSuggestions(vSuggestions)

            Catch ex As Exception
                Console.WriteLine($"AddScopeTypeSuggestions error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Adds top-level namespaces (System, Microsoft, etc.) from referenced assemblies
        ''' </summary>
        ''' <remarks>
        ''' Results are cached (see pTopLevelNamespaceCache, cleared in ClearReferences) since
        ''' this runs on every keystroke while a type/namespace is expected and scanning
        ''' GetExportedTypes() across mscorlib-sized assemblies on every call would be slow
        ''' </remarks>
        Private Sub AddFrameworkNamespaceSuggestions(vSuggestions As List(Of CodeSenseSuggestion))
            Try
                If pTopLevelNamespaceCache Is Nothing Then
                    BuildTopLevelNamespaceCache()
                End If

                for each lNamespace in pTopLevelNamespaceCache
                    Dim lSuggestion As New CodeSenseSuggestion()
                    lSuggestion.Text = lNamespace
                    lSuggestion.Kind = CodeSenseSuggestionKind.eNamespace
                    lSuggestion.Icon = "namespace"
                    lSuggestion.Description = $"Namespace {lNamespace}"
                    vSuggestions.Add(lSuggestion)
                Next

            Catch ex As Exception
                Console.WriteLine($"AddFrameworkNamespaceSuggestions error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Scans all referenced assemblies for their top-level namespace segments
        ''' </summary>
        Private Sub BuildTopLevelNamespaceCache()
            Try
                Dim lNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

                If pProjectReferences IsNot Nothing Then
                    For Each lAssembly As Assembly In pProjectReferences
                        Try
                            For Each lType As Type In lAssembly.GetExportedTypes()
                                If Not String.IsNullOrEmpty(lType.Namespace) Then
                                    Dim lDotIndex As Integer = lType.Namespace.IndexOf("."c)
                                    Dim lTopLevel As String = If(lDotIndex > 0, lType.Namespace.Substring(0, lDotIndex), lType.Namespace)
                                    lNames.Add(lTopLevel)
                                End If
                            Next
                        Catch ex As Exception
                            Console.WriteLine($"BuildTopLevelNamespaceCache: failed to scan {lAssembly.GetName().Name}: {ex.Message}")
                        End Try
                    Next
                End If

                pTopLevelNamespaceCache = New List(Of String)(lNames)

            Catch ex As Exception
                Console.WriteLine($"BuildTopLevelNamespaceCache error: {ex.Message}")
                pTopLevelNamespaceCache = New List(Of String)()
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

                ' Track names already added - partial classes (mandatory pattern in this
                ' project, e.g. the MainWindow.*.vb files) contribute one node per declaring
                ' file, so without this the same class name would be suggested once per file
                Dim lSeenNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

                ' Walk the tree looking for types
                AddProjectTypesRecursive(pProjectSyntaxTree, vSuggestions, lSeenNames)

            Catch ex As Exception
                Console.WriteLine($"AddProjectTypeSuggestions error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Recursively add project types
        ''' </summary>
        Private Sub AddProjectTypesRecursive(vNode As SyntaxNode, vSuggestions As List(Of CodeSenseSuggestion), vSeenNames As HashSet(Of String))
            Try
                If vNode Is Nothing Then Return

                Select Case vNode.NodeType
                    Case CodeNodeType.eClass
                        If vSeenNames.Add(vNode.Name) Then
                            Dim lSuggestion As New CodeSenseSuggestion()
                            lSuggestion.Text = vNode.Name
                            lSuggestion.Kind = CodeSenseSuggestionKind.eClass
                            lSuggestion.Icon = "class"
                            lSuggestion.Description = $"Class {vNode.Name}"
                            vSuggestions.Add(lSuggestion)
                        End If

                    Case CodeNodeType.eInterface
                        If vSeenNames.Add(vNode.Name) Then
                            Dim lSuggestion As New CodeSenseSuggestion()
                            lSuggestion.Text = vNode.Name
                            lSuggestion.Kind = CodeSenseSuggestionKind.eInterface
                            lSuggestion.Icon = "interface"
                            lSuggestion.Description = $"Interface {vNode.Name}"
                            vSuggestions.Add(lSuggestion)
                        End If

                    Case CodeNodeType.eNamespace
                        ' Includes the project's own root namespace (synthesized as an
                        ' eNamespace node by ProjectManager - see GetProjectSyntaxTree) as
                        ' well as any explicit sub-namespaces (e.g. "Namespace Editors")
                        If vSeenNames.Add(vNode.Name) Then
                            Dim lSuggestion As New CodeSenseSuggestion()
                            lSuggestion.Text = vNode.Name
                            lSuggestion.Kind = CodeSenseSuggestionKind.eNamespace
                            lSuggestion.Icon = "namespace"
                            lSuggestion.Description = $"Namespace {vNode.Name}"
                            vSuggestions.Add(lSuggestion)
                        End If
                End Select

                ' Recursively process children
                If vNode.Children IsNot Nothing Then
                    for each lChild in vNode.Children
                        AddProjectTypesRecursive(lChild, vSuggestions, vSeenNames)
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
        ''' Find the innermost method/function/constructor/property containing the specified line
        ''' </summary>
        ''' <param name="vNode">Node to search from (typically the document's file-root node)</param>
        ''' <param name="vLine">0-based line number to locate</param>
        ''' <returns>The innermost containing member node, or Nothing if the line isn't inside one</returns>
        Private Function FindContainingMethod(vNode As SyntaxNode, vLine As Integer) As SyntaxNode
            Try
                If vNode Is Nothing Then Return Nothing

                If vNode.NodeType = CodeNodeType.eMethod OrElse vNode.NodeType = CodeNodeType.eFunction OrElse
                   vNode.NodeType = CodeNodeType.eConstructor OrElse vNode.NodeType = CodeNodeType.eProperty Then
                    If vNode.StartLine <= vLine AndAlso vNode.EndLine >= vLine Then
                        Return vNode
                    End If
                End If

                If vNode.Children IsNot Nothing Then
                    for each lChild in vNode.Children
                        Dim lResult = FindContainingMethod(lChild, vLine)
                        If lResult IsNot Nothing Then
                            Return lResult
                        End If
                    Next
                End If

                Return Nothing

            Catch ex As Exception
                Console.WriteLine($"FindContainingMethod error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        ''' <summary>
        ''' Populates Imports, containing class/method, and locally-visible parameters/variables
        ''' on the context by walking the current document's syntax tree
        ''' </summary>
        ''' <param name="vContext">The context being built for this suggestion request</param>
        ''' <remarks>
        ''' Runs on every GetSuggestions call so all trigger kinds (dot, space, manual) see live
        ''' scope data instead of the empty defaults CodeSenseContext.New() leaves them at
        ''' </remarks>
        Private Sub PopulateScopeContext(vContext As CodeSenseContext)
            Try
                If pCurrentSyntaxTree Is Nothing Then Return

                ' Imports: the file-root node's direct children include one eImport node
                ' per imported namespace (see RoslynConverter.ProcessImport)
                If pCurrentSyntaxTree.Children IsNot Nothing Then
                    Dim lImports As New List(Of String)()
                    for each lChild in pCurrentSyntaxTree.Children
                        If lChild.NodeType = CodeNodeType.eImport AndAlso Not String.IsNullOrEmpty(lChild.Name) Then
                            If Not lImports.Contains(lChild.Name) Then
                                lImports.Add(lChild.Name)
                            End If
                        End If
                    Next
                    vContext.ImportsContext = lImports
                End If

                ' Containing class, for Me./MyBase. and general scope awareness
                Dim lClassNode As SyntaxNode = FindContainingClass(pCurrentSyntaxTree, vContext.TriggerPosition.Line)
                If lClassNode IsNot Nothing Then
                    vContext.ContainingClass = lClassNode.Name
                End If

                ' Containing method/function/constructor/property, for locals + parameters
                Dim lMethodNode As SyntaxNode = FindContainingMethod(pCurrentSyntaxTree, vContext.TriggerPosition.Line)
                If lMethodNode IsNot Nothing Then
                    vContext.ContainingMethod = lMethodNode.Name

                    Dim lLocals As New List(Of String)()

                    ' Parameters are in scope for the whole member body
                    If lMethodNode.Parameters IsNot Nothing Then
                        for each lParam in lMethodNode.Parameters
                            If Not String.IsNullOrEmpty(lParam.Name) Then
                                lLocals.Add(lParam.Name)
                            End If
                        Next
                    End If

                    ' Dim'd locals - RoslynConverter.ExtractLocalVariables flattens every local
                    ' in the member as a direct eVariable child regardless of nested block, so
                    ' restrict to ones declared at or before the cursor line as a simple proxy
                    ' for "already in scope" rather than showing locals from later in the method
                    If lMethodNode.Children IsNot Nothing Then
                        for each lChild in lMethodNode.Children
                            If lChild.NodeType = CodeNodeType.eVariable AndAlso lChild.StartLine <= vContext.TriggerPosition.Line Then
                                If Not String.IsNullOrEmpty(lChild.Name) AndAlso Not lLocals.Contains(lChild.Name) Then
                                    lLocals.Add(lChild.Name)
                                End If
                            End If
                        Next
                    End If

                    vContext.LocalVariables = lLocals
                End If

            Catch ex As Exception
                Console.WriteLine($"PopulateScopeContext error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Resolves the declared type of an identifier (parameter, local variable, field, or
        ''' property) visible at the cursor's position
        ''' </summary>
        ''' <param name="vIdentifier">Identifier to resolve, as typed by the user</param>
        ''' <param name="vContext">Current suggestion context, used for the cursor position</param>
        ''' <returns>The declared type name, or Nothing if not found</returns>
        ''' <remarks>
        ''' This is a syntactic lookup against data RoslynConverter already extracts
        ''' (ParameterInfo.ParameterType, SyntaxNode.DataType) - it resolves explicitly-typed
        ''' declarations ("Dim lFoo As Bar", "vFoo As Bar") correctly, but Option Infer-style
        ''' declarations with no As clause are recorded as "Object" and can't be resolved this
        ''' way. Real inference for those would need a Roslyn semantic model lookup, which
        ''' CodeSenseEngine does not currently have access to.
        ''' </remarks>
        Private Function ResolveVariableType(vIdentifier As String, vContext As CodeSenseContext) As String
            Try
                If String.IsNullOrEmpty(vIdentifier) OrElse pCurrentSyntaxTree Is Nothing Then Return Nothing

                Dim lMethodNode As SyntaxNode = FindContainingMethod(pCurrentSyntaxTree, vContext.TriggerPosition.Line)
                If lMethodNode IsNot Nothing Then
                    If lMethodNode.Parameters IsNot Nothing Then
                        for each lParam in lMethodNode.Parameters
                            If String.Equals(lParam.Name, vIdentifier, StringComparison.OrdinalIgnoreCase) Then
                                Return lParam.ParameterType
                            End If
                        Next
                    End If

                    If lMethodNode.Children IsNot Nothing Then
                        for each lChild in lMethodNode.Children
                            If lChild.NodeType = CodeNodeType.eVariable AndAlso
                               String.Equals(lChild.Name, vIdentifier, StringComparison.OrdinalIgnoreCase) Then
                                Return lChild.DataType
                            End If
                        Next
                    End If
                End If

                Dim lClassNode As SyntaxNode = FindContainingClass(pCurrentSyntaxTree, vContext.TriggerPosition.Line)
                If lClassNode IsNot Nothing AndAlso lClassNode.Children IsNot Nothing Then
                    for each lChild in lClassNode.Children
                        If (lChild.NodeType = CodeNodeType.eField OrElse lChild.NodeType = CodeNodeType.eProperty) AndAlso
                           String.Equals(lChild.Name, vIdentifier, StringComparison.OrdinalIgnoreCase) Then
                            Return If(Not String.IsNullOrEmpty(lChild.DataType), lChild.DataType, lChild.ReturnType)
                        End If
                    Next
                End If

                Return Nothing

            Catch ex As Exception
                Console.WriteLine($"ResolveVariableType error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        ''' <summary>
        ''' Recursively find a node by name
        ''' </summary>
        Private Function FindNodeByNameRecursive(vNode As SyntaxNode, vName As String) As SyntaxNode
            Try
                If vNode Is Nothing Then Return Nothing

                ' Only match type-like/namespace nodes - the project tree's root is an
                ' eDocument wrapper named after the project (e.g. "SimpleIDE"), which can
                ' collide with the real eNamespace child of the same name (the project's root
                ' namespace); without this check that wrapper matches first and member lookup
                ' silently returns an eDocument node with no usable members
                Dim lIsCompletableKind As Boolean =
                    vNode.NodeType = CodeNodeType.eClass OrElse
                    vNode.NodeType = CodeNodeType.eInterface OrElse
                    vNode.NodeType = CodeNodeType.eModule OrElse
                    vNode.NodeType = CodeNodeType.eStructure OrElse
                    vNode.NodeType = CodeNodeType.eEnum OrElse
                    vNode.NodeType = CodeNodeType.eNamespace

                If lIsCompletableKind AndAlso String.Equals(vNode.Name, vName, StringComparison.OrdinalIgnoreCase) Then
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
        ''' <param name="vNode">The member node being considered as a suggestion</param>
        ''' <returns>True if visible from the class the cursor is currently in (see pCurrentContext.ContainingClass)</returns>
        ''' <remarks>
        ''' Public/Friend members are always visible (CodeSense only surfaces same-project symbols
        ''' via this path, so "Friend" effectively means "same assembly" here). Private members
        ''' require the caller to be inside the exact same class; Protected members require the
        ''' caller's class to be the same class or a subclass of it (checked via IsClassDerivedFrom).
        ''' </remarks>
        Private Function IsNodeAccessible(vNode As SyntaxNode) As Boolean
            Try
                If vNode Is Nothing Then Return False

                If vNode.IsPublic OrElse vNode.IsFriend Then Return True

                If Not vNode.IsPrivate AndAlso Not vNode.IsProtected Then
                    ' No explicit accessibility modifier captured - fail open rather than
                    ' hide a member we can't actually classify
                    Return True
                End If

                Dim lOwnerType As SyntaxNode = GetContainingTypeNode(vNode)
                If lOwnerType Is Nothing Then Return True

                Dim lCallerClassName As String = pCurrentContext?.ContainingClass
                If String.IsNullOrEmpty(lCallerClassName) Then Return False

                If String.Equals(lCallerClassName, lOwnerType.Name, StringComparison.OrdinalIgnoreCase) Then
                    Return True
                End If

                If vNode.IsProtected Then
                    Return IsClassDerivedFrom(lCallerClassName, lOwnerType.Name)
                End If

                Return False

            Catch ex As Exception
                Console.WriteLine($"IsNodeAccessible error: {ex.Message}")
                Return True
            End Try
        End Function

        ''' <summary>
        ''' Walks up from a member node to find its containing class/module/structure/interface
        ''' </summary>
        ''' <param name="vNode">Member node to walk up from</param>
        ''' <returns>The nearest type-level ancestor, or Nothing if none is found</returns>
        Private Function GetContainingTypeNode(vNode As SyntaxNode) As SyntaxNode
            Try
                Dim lCurrent As SyntaxNode = vNode?.Parent
                While lCurrent IsNot Nothing
                    If lCurrent.NodeType = CodeNodeType.eClass OrElse lCurrent.NodeType = CodeNodeType.eModule OrElse
                       lCurrent.NodeType = CodeNodeType.eStructure OrElse lCurrent.NodeType = CodeNodeType.eInterface Then
                        Return lCurrent
                    End If
                    lCurrent = lCurrent.Parent
                End While
                Return Nothing

            Catch ex As Exception
                Console.WriteLine($"GetContainingTypeNode error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        ''' <summary>
        ''' Finds a Class/Module/Structure/Interface node by name anywhere in the project tree
        ''' </summary>
        ''' <param name="vName">Simple type name to search for</param>
        ''' <returns>The matching type node, or Nothing if not found</returns>
        Private Function FindTypeNodeByName(vName As String) As SyntaxNode
            Try
                If String.IsNullOrEmpty(vName) Then Return Nothing

                If pProjectSyntaxTree IsNot Nothing Then
                    Dim lResult = FindTypeNodeByNameRecursive(pProjectSyntaxTree, vName)
                    If lResult IsNot Nothing Then Return lResult
                End If

                If pCurrentSyntaxTree IsNot Nothing Then
                    Return FindTypeNodeByNameRecursive(pCurrentSyntaxTree, vName)
                End If

                Return Nothing

            Catch ex As Exception
                Console.WriteLine($"FindTypeNodeByName error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        ''' <summary>
        ''' Recursive helper for FindTypeNodeByName
        ''' </summary>
        Private Function FindTypeNodeByNameRecursive(vNode As SyntaxNode, vName As String) As SyntaxNode
            Try
                If vNode Is Nothing Then Return Nothing

                If (vNode.NodeType = CodeNodeType.eClass OrElse vNode.NodeType = CodeNodeType.eModule OrElse
                    vNode.NodeType = CodeNodeType.eStructure OrElse vNode.NodeType = CodeNodeType.eInterface) AndAlso
                   String.Equals(vNode.Name, vName, StringComparison.OrdinalIgnoreCase) Then
                    Return vNode
                End If

                If vNode.Children IsNot Nothing Then
                    for each lChild in vNode.Children
                        Dim lResult = FindTypeNodeByNameRecursive(lChild, vName)
                        If lResult IsNot Nothing Then Return lResult
                    Next
                End If

                Return Nothing

            Catch ex As Exception
                Console.WriteLine($"FindTypeNodeByNameRecursive error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        ''' <summary>
        ''' Checks whether vDerivedClassName inherits (directly or transitively) from vBaseClassName
        ''' </summary>
        ''' <param name="vDerivedClassName">Name of the potential subclass</param>
        ''' <param name="vBaseClassName">Name of the potential base class</param>
        ''' <returns>True if vDerivedClassName's Inherits chain reaches vBaseClassName</returns>
        ''' <remarks>
        ''' Only walks project-defined classes (via BaseType/FindTypeNodeByName); a chain that
        ''' leaves the project into a framework base type simply stops there. Bounded to 25 hops
        ''' as a guard against bad/cyclic data rather than genuine deep hierarchies.
        ''' </remarks>
        Private Function IsClassDerivedFrom(vDerivedClassName As String, vBaseClassName As String) As Boolean
            Try
                If String.IsNullOrEmpty(vDerivedClassName) OrElse String.IsNullOrEmpty(vBaseClassName) Then Return False

                Dim lCurrentName As String = vDerivedClassName
                Dim lGuard As Integer = 0
                While lGuard < 25
                    lGuard += 1

                    Dim lNode As SyntaxNode = FindTypeNodeByName(lCurrentName)
                    If lNode Is Nothing OrElse String.IsNullOrEmpty(lNode.BaseType) Then Return False

                    ' BaseType may be namespace-qualified (e.g. "SimpleIDE.Editors.Foo")
                    Dim lBaseSimpleName As String = lNode.BaseType
                    Dim lDotIndex As Integer = lBaseSimpleName.LastIndexOf("."c)
                    If lDotIndex >= 0 Then lBaseSimpleName = lBaseSimpleName.Substring(lDotIndex + 1)

                    If String.Equals(lBaseSimpleName, vBaseClassName, StringComparison.OrdinalIgnoreCase) Then
                        Return True
                    End If

                    lCurrentName = lBaseSimpleName
                End While

                Return False

            Catch ex As Exception
                Console.WriteLine($"IsClassDerivedFrom error: {ex.Message}")
                Return False
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
