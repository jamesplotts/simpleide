' Syntax/SyntaxNode.vb - VB.NET syntax tree node representation
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Models

Namespace Syntax
    
    ' Represents a node in the VB.NET syntax tree
    Partial Public Class SyntaxNode
        
        ' Node identification
        Public Property NodeId As String
        Public Property NodeType As CodeNodeType
        Public Property Name As String
        Public Property FullName As String
        Public Property InitialValue as String
        
        ' Location information
        Public Property StartLine As Integer
        Public Property EndLine As Integer
        Public Property StartColumn As Integer
        Public Property EndColumn As Integer
        
        ' Hierarchy
        Public Property Parent As SyntaxNode
        Public Property Children As New List(Of SyntaxNode)
        Public Property Level As Integer = 0
        
        ' Node metadata
        Public Property Attributes As New Dictionary(Of String, String)
        Public Property IsPartial As Boolean = False
        Public Property IsPublic As Boolean = True
        Public Property IsPrivate As Boolean = False
        Public Property IsProtected As Boolean = False
        Public Property IsFriend As Boolean = False
        Public Property IsShared As Boolean = False
        Public Property IsOverridable As Boolean = False
        Public Property IsOverrides As Boolean = False
        Public Property IsMustOverride As Boolean = False
        Public Property IsNotOverridable As Boolean = False
        Public Property IsMustInherit As Boolean = False
        Public Property IsNotInheritable As Boolean = False
        Public Property IsReadOnly As Boolean = False
        Public Property IsWriteOnly As Boolean = False
        Public Property IsConst As Boolean = False
        Public Property IsSealed As Boolean = False
        Public Property IsAbstract As Boolean = False
        Public Property IsIterator As Boolean = False
        Public Property IsShadows As Boolean = False
        Public Property IsAsync As Boolean = False
        Public Property IsWithEvents As Boolean = False
        Public Property IsStatic As Boolean = False
        Public Property IsAutoImplemented as Boolean = False

        Public Property Visibility As eVisibility = eVisibility.ePublic
        Public Property FilePath As String = ""
        Public Property DataType As String = ""

        ' Additional properties for specific node types
        Public Property ReturnType As String = ""          ' for functions/properties
        Public Property Parameters As New List(Of ParameterInfo)  ' for methods/functions
        Public Property BaseType As String = ""            ' for classes/interfaces
        Public Property ImplementsList As New List(Of String)     ' for classes implementing interfaces
        Public Property InheritsList As New List(Of String)       ' for classes inheriting

        Public Property XmlDocumentation as New XMLDocInfo

        
        Public Enum eVisibility
            ePublic
            ePrivate
            eFriend
            eProtected
            eProtectedFriend
            eUnspecified
        End Enum

        ' Constructor
        Public Sub New()
            NodeId = Guid.NewGuid().ToString()
        End Sub
        
        Public Sub New(vNodeType As CodeNodeType, vName As String)
            Me.New()
            NodeType = vNodeType
            Name = vName
            FullName = vName
        End Sub
        
        ' Add child node
        Public Sub AddChild(vChild As SyntaxNode)
            If vChild Is Nothing Then Return
            
            vChild.Parent = Me
            vChild.Level = Level + 1
            Children.Add(vChild)
            
            ' Update full name based on parent
            If Not String.IsNullOrEmpty(FullName) Then
                vChild.FullName = FullName & "." & vChild.Name
            End If
        End Sub
        
        ' Remove child node
        Public Sub RemoveChild(vChild As SyntaxNode)
            If vChild Is Nothing Then Return
            
            Children.Remove(vChild)
            vChild.Parent = Nothing
        End Sub
        
        ''' <summary>
        ''' Find child node by name using case-insensitive comparison
        ''' </summary>
        ''' <param name="vName">Name to search for</param>
        ''' <returns>The found node or Nothing</returns>
        Public Function FindChild(vName As String) As SyntaxNode
            ' FIXED: Use case-insensitive comparison for name matching
            for each lChild in Children
                If String.Equals(lChild.Name, vName, StringComparison.OrdinalIgnoreCase) Then
                    Return lChild
                End If
            Next
            Return Nothing
        End Function
        
        ' Find all children of specific type
        Public Function FindChildrenOfType(vNodeType As CodeNodeType) As List(Of SyntaxNode)
            Dim lResult As New List(Of SyntaxNode)
            
            for each lChild in Children
                If lChild.NodeType = vNodeType Then
                    lResult.Add(lChild)
                End If
            Next
            
            Return lResult
        End Function
        
        ' Get all descendants (recursive)
        Public Function GetAllDescendants() As List(Of SyntaxNode)
            Dim lResult As New List(Of SyntaxNode)
            
            for each lChild in Children
                lResult.Add(lChild)
                lResult.AddRange(lChild.GetAllDescendants())
            Next
            
            Return lResult
        End Function
        
        ' Get display text for UI
        Public Function GetDisplayText() As String
            Dim lText As String = Name
            
            Select Case NodeType
                Case CodeNodeType.eMethod, CodeNodeType.eConstructor
                    lText &= "("
                    If Parameters.Count > 0 Then
                        Dim lParams As New List(Of String)
                        for each lParam in Parameters
                            lParams.Add(lParam.GetDisplayText())
                        Next
                        lText &= String.Join(", ", lParams)
                    End If
                    lText &= ")"
                    
                Case CodeNodeType.eFunction
                    lText &= "("
                    If Parameters.Count > 0 Then
                        Dim lParams As New List(Of String)
                        for each lParam in Parameters
                            lParams.Add(lParam.GetDisplayText())
                        Next
                        lText &= String.Join(", ", lParams)
                    End If
                    lText &= ")"
                    If Not String.IsNullOrEmpty(ReturnType) Then
                        lText &= " As " & ReturnType
                    End If
                    
                Case CodeNodeType.eProperty
                    If Not String.IsNullOrEmpty(ReturnType) Then
                        lText &= " As " & ReturnType
                    End If
                    
                Case CodeNodeType.eField, CodeNodeType.eVariable
                    If Not String.IsNullOrEmpty(ReturnType) Then
                        lText &= " As " & ReturnType
                    End If
                    
                Case CodeNodeType.eClass
                    If Not String.IsNullOrEmpty(BaseType) Then
                        lText &= " (Inherits " & BaseType & ")"
                    End If
                    
                Case CodeNodeType.eInterface
                    If InheritsList.Count > 0 Then
                        lText &= " (Inherits " & String.Join(", ", InheritsList) & ")"
                    End If
            End Select
            
            Return lText
        End Function
        
        ' Get icon name for node type
        Public Function GetIconName() As String
            Select Case NodeType
                Case CodeNodeType.eNamespace
                    Return "folder-symbolic"
                Case CodeNodeType.eClass
                    Return "application-x-executable-symbolic"
                Case CodeNodeType.eModule
                    Return "Package-x-generic-symbolic"
                Case CodeNodeType.eInterface
                    Return "emblem-shared-symbolic"
                Case CodeNodeType.eStructure
                    Return "view-list-symbolic"
                Case CodeNodeType.eEnum
                    Return "format-justify-left-symbolic"
                Case CodeNodeType.eMethod, CodeNodeType.eConstructor
                    Return "system-run-symbolic"
                Case CodeNodeType.eFunction
                    Return "accessories-calculator-symbolic"
                Case CodeNodeType.eProperty
                    Return "document-properties-symbolic"
                Case CodeNodeType.eField
                    Return "insert-object-symbolic"
                Case CodeNodeType.eEvent
                    Return "starred-symbolic"
                Case CodeNodeType.eVariable, CodeNodeType.eParameter
                    Return "format-indent-more-symbolic"
                Case Else
                    Return "Text-x-generic-symbolic"
            End Select
        End Function
        
        ' Clone the node (shallow copy)
        Public Function Clone() As SyntaxNode
            Dim lClone As New SyntaxNode()
            
            ' Copy basic properties
            lClone.NodeType = NodeType
            lClone.Name = Name
            lClone.FullName = FullName
            lClone.StartLine = StartLine
            lClone.EndLine = EndLine
            lClone.StartColumn = StartColumn
            lClone.EndColumn = EndColumn
            lClone.Level = Level
            
            ' Copy flags
            lClone.IsPartial = IsPartial
            lClone.IsPublic = IsPublic
            lClone.IsPrivate = IsPrivate
            lClone.IsProtected = IsProtected
            lClone.IsFriend = IsFriend
            lClone.IsShared = IsShared
            lClone.IsOverridable = IsOverridable
            lClone.IsOverrides = IsOverrides
            lClone.IsMustOverride = IsMustOverride
            lClone.IsNotOverridable = IsNotOverridable
            lClone.IsMustInherit = IsMustInherit
            lClone.IsNotInheritable = IsNotInheritable
            
            ' Copy additional properties
            lClone.ReturnType = ReturnType
            lClone.BaseType = BaseType
            
            ' Copy collections (shallow)
            for each lAttr in Attributes
                lClone.Attributes.Add(lAttr.key, lAttr.Value)
            Next
            
            for each lParam in Parameters
                lClone.Parameters.Add(lParam.Clone())
            Next
            
            lClone.ImplementsList.AddRange(ImplementsList)
            lClone.InheritsList.AddRange(InheritsList)
            
            ' Note: Parent and Children are not cloned
            
            Return lClone
        End Function

        ''' <summary>
        ''' Fixed version of CopyNodeAttributes that properly initializes Attributes
        ''' </summary>
        Public Sub CopyNodeAttributesTo(vTarget As SyntaxNode)
            Dim vSource As SyntaxNode = Me
            Try
                vTarget.StartLine = vSource.StartLine
                vTarget.EndLine = vSource.EndLine
                vTarget.StartColumn = vSource.StartColumn
                vTarget.EndColumn = vSource.EndColumn
                vTarget.IsPublic = vSource.IsPublic
                vTarget.IsPrivate = vSource.IsPrivate
                vTarget.IsProtected = vSource.IsProtected
                vTarget.IsFriend = vSource.IsFriend
                vTarget.IsPartial = vSource.IsPartial
                vTarget.IsShared = vSource.IsShared
                vTarget.IsOverridable = vSource.IsOverridable
                vTarget.IsOverrides = vSource.IsOverrides
                vTarget.IsMustOverride = vSource.IsMustOverride
                vTarget.IsNotOverridable = vSource.IsNotOverridable
                vTarget.IsReadOnly = vSource.IsReadOnly
                vTarget.IsWriteOnly = vSource.IsWriteOnly
                vTarget.IsConst = vSource.IsConst
                vTarget.IsWithEvents = vSource.IsWithEvents
                vTarget.Visibility = vSource.Visibility
                vTarget.ReturnType = vSource.ReturnType
                vTarget.BaseType = vSource.BaseType
                vTarget.IsImplicit = vSource.IsImplicit
                
                ' Copy parameters
                for each lParam in vSource.Parameters
                    vTarget.Parameters.Add(lParam.Clone())
                Next
                
                ' Copy lists
                vTarget.ImplementsList.AddRange(vSource.ImplementsList)
                vTarget.InheritsList.AddRange(vSource.InheritsList)
                
                ' Initialize target Attributes if needed
                If vTarget.Attributes Is Nothing Then
                    vTarget.Attributes = New Dictionary(Of String, String)()
                End If
                
                ' Copy attributes dictionary (except FilePath which is set separately)
                If vSource.Attributes IsNot Nothing Then
                    for each lKvp in vSource.Attributes
                        If lKvp.Key <> "FilePath" AndAlso lKvp.Key <> "FilePaths" Then
                            vTarget.Attributes(lKvp.Key) = lKvp.Value
                        End If
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CopyNodeAttributes error: {ex.Message}")
            End Try
        End Sub

        ' ===== Additional Properties for Namespace Organization =====
    
        ''' <summary>
        ''' Indicates if this is an implicit namespace node (from root namespace)
        ''' </summary>
        Public Property IsImplicit As Boolean = False
        
        ''' <summary>
        ''' Metadata storage for additional information (like file paths)
        ''' </summary>
        Public Property Metadata As Dictionary(Of String, Object)
        
        ''' <summary>
        ''' Get the fully qualified name of this node
        ''' </summary>
        Public Function GetFullyQualifiedName() As String
            Try
                Dim lParts As New List(Of String)()
                Dim lCurrent As SyntaxNode = Me
                
                ' Build name from this node up to root
                While lCurrent IsNot Nothing
                    If lCurrent.NodeType = CodeNodeType.eNamespace OrElse
                       lCurrent.NodeType = CodeNodeType.eClass OrElse
                       lCurrent.NodeType = CodeNodeType.eModule OrElse
                       lCurrent.NodeType = CodeNodeType.eInterface OrElse
                       lCurrent.NodeType = CodeNodeType.eStructure OrElse
                       lCurrent.NodeType = CodeNodeType.eEnum Then
                        
                        ' Don't include implicit namespaces in the display
                        If Not (lCurrent.NodeType = CodeNodeType.eNamespace AndAlso lCurrent.IsImplicit) Then
                            lParts.Insert(0, lCurrent.Name)
                        End If
                    End If
                    
                    lCurrent = lCurrent.Parent
                End While
                
                Return String.Join(".", lParts)
                
            Catch ex As Exception
                Console.WriteLine($"GetFullyQualifiedName error: {ex.Message}")
                Return Name
            End Try
        End Function
        
        ''' <summary>
        ''' Get the file path associated with this node (if any)
        ''' </summary>
        Public Function GetFilePath() As String
            Try
                If Metadata IsNot Nothing AndAlso Metadata.ContainsKey("FilePath") Then
                    Return CStr(Metadata("FilePath"))
                End If
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"GetFilePath error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ' To: SyntaxNode.vb
        ''' <summary>
        ''' Find child node by name and type using case-insensitive comparison
        ''' </summary>
        ''' <param name="vName">Name to search for</param>
        ''' <param name="vNodeType">Type to search for</param>
        ''' <returns>The found node or Nothing</returns>
        Public Function FindChild(vName As String, vNodeType As CodeNodeType) As SyntaxNode
            Try
                ' FIXED: Use case-insensitive comparison for name matching
                for each lChild in Children
                    If String.Equals(lChild.Name, vName, StringComparison.OrdinalIgnoreCase) AndAlso 
                       lChild.NodeType = vNodeType Then
                        Return lChild
                    End If
                Next
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"FindChild error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Get all descendants of a specific type
        ''' </summary>
        Public Function GetDescendantsOfType(vNodeType As CodeNodeType) As List(Of SyntaxNode)
            Dim lResults As New List(Of SyntaxNode)()
            Try
                CollectDescendantsOfType(vNodeType, lResults)
                Return lResults
                
            Catch ex As Exception
                Console.WriteLine($"GetDescendantsOfType error: {ex.Message}")
                Return lResults
            End Try
        End Function
        
        Private Sub CollectDescendantsOfType(vNodeType As CodeNodeType, vResults As List(Of SyntaxNode))
            Try
                If Me.NodeType = vNodeType Then
                    vResults.Add(Me)
                End If
                
                for each lChild in Children
                    lChild.CollectDescendantsOfType(vNodeType, vResults)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"CollectDescendantsOfType error: {ex.Message}")
            End Try
        End Sub

        
        ''' <summary>
        ''' The XML documentation summary for this node
        ''' </summary>
        Public Property Summary As String
        
        ''' <summary>
        ''' Parameter documentation (for methods/functions)
        ''' Key: Parameter name, Value: Description
        ''' </summary>
        Public Property ParamDocs As Dictionary(Of String, String)
        
        ''' <summary>
        ''' Return value documentation (for functions/properties)
        ''' </summary>
        Public Property Returns As String
        
        ''' <summary>
        ''' Remarks/additional documentation
        ''' </summary>
        Public Property Remarks As String
        
        ''' <summary>
        ''' Example usage
        ''' </summary>
        Public Property Example As String



        
        ''' <summary>
        ''' Get formatted documentation for CodeSense tooltip
        ''' </summary>
        Public Function GetCodeSenseTooltip() As String
            Dim lTooltip As String = ""
            
            ' First line: Full declaration
            lTooltip = GetFullDeclaration()
            
            ' Add summary if available
            If Not String.IsNullOrWhiteSpace(Summary) Then
                lTooltip &= Environment.NewLine & Environment.NewLine
                lTooltip &= Summary
            End If
            
            ' Add parameter documentation
            If ParamDocs IsNot Nothing AndAlso ParamDocs.Count > 0 Then
                lTooltip &= Environment.NewLine & Environment.NewLine & "Parameters:"
                for each lParam in ParamDocs
                    lTooltip &= Environment.NewLine & $"  {lParam.Key}: {lParam.Value}"
                Next
            End If
            
            ' Add return documentation
            If Not String.IsNullOrWhiteSpace(Returns) Then
                lTooltip &= Environment.NewLine & Environment.NewLine & "Returns:"
                lTooltip &= Environment.NewLine & $"  {Returns}"
            End If
            
            ' Add remarks if available
            If Not String.IsNullOrWhiteSpace(Remarks) Then
                lTooltip &= Environment.NewLine & Environment.NewLine & "Remarks:"
                lTooltip &= Environment.NewLine & Remarks
            End If
            
            Return lTooltip
        End Function
        
        ''' <summary>
        ''' Get the full declaration string for this node
        ''' </summary>
        Public Function GetFullDeclaration() As String
            Select Case NodeType
                Case CodeNodeType.eMethod, CodeNodeType.eConstructor
                    Return GetMethodDeclaration()
                Case CodeNodeType.eFunction
                    Return GetFunctionDeclaration()
                Case CodeNodeType.eProperty
                    Return GetPropertyDeclaration()
                Case CodeNodeType.eClass
                    Return GetClassDeclaration()
                Case CodeNodeType.eInterface
                    Return GetInterfaceDeclaration()
                Case CodeNodeType.eField
                    Return GetFieldDeclaration()
                Case CodeNodeType.eEvent
                    Return GetEventDeclaration()
                Case Else
                    Return Name
            End Select
        End Function
        
        Private Function GetMethodDeclaration() As String
            Dim lDecl As String = GetVisibilityString() & " "
            
            If IsShared Then lDecl &= "Shared "
            If IsOverridable Then lDecl &= "Overridable "
            If IsOverrides Then lDecl &= "Overrides "
            
            lDecl &= "Sub " & Name & "("
            
            If Parameters IsNot Nothing AndAlso Parameters.Count > 0 Then
                Dim lParams As New List(Of String)()
                for each lParam in Parameters
                    Dim lParamStr As String = lParam.Name
                    If Not String.IsNullOrEmpty(lParam.ParameterType) Then
                        lParamStr &= " As " & lParam.ParameterType
                    End If
                    lParams.Add(lParamStr)
                Next
                lDecl &= String.Join(", ", lParams)
            End If
            
            lDecl &= ")"
            Return lDecl
        End Function
        
        Private Function GetFunctionDeclaration() As String
            Dim lDecl As String = GetVisibilityString() & " "
            
            If IsShared Then lDecl &= "Shared "
            If IsOverridable Then lDecl &= "Overridable "
            If IsOverrides Then lDecl &= "Overrides "
            
            lDecl &= "Function " & Name & "("
            
            If Parameters IsNot Nothing AndAlso Parameters.Count > 0 Then
                Dim lParams As New List(Of String)()
                for each lParam in Parameters
                    Dim lParamStr As String = lParam.Name
                    If Not String.IsNullOrEmpty(lParam.ParameterType) Then
                        lParamStr &= " As " & lParam.ParameterType
                    End If
                    lParams.Add(lParamStr)
                Next
                lDecl &= String.Join(", ", lParams)
            End If
            
            lDecl &= ")"
            
            If Not String.IsNullOrEmpty(ReturnType) Then
                lDecl &= " As " & ReturnType
            End If
            
            Return lDecl
        End Function
        
        Private Function GetPropertyDeclaration() As String
            Dim lDecl As String = GetVisibilityString() & " "
            
            If IsShared Then lDecl &= "Shared "
            If IsReadOnly Then lDecl &= "ReadOnly "
            If IsWriteOnly Then lDecl &= "WriteOnly "
            
            lDecl &= "Property " & Name
            
            If Not String.IsNullOrEmpty(ReturnType) Then
                lDecl &= " As " & ReturnType
            End If
            
            Return lDecl
        End Function
        
        Private Function GetClassDeclaration() As String
            Dim lDecl As String = GetVisibilityString() & " "
            
            If IsPartial Then lDecl &= "Partial "
            If IsMustInherit Then lDecl &= "MustInherit "
            If IsNotInheritable Then lDecl &= "NotInheritable "
            
            lDecl &= "Class " & Name
            
            If Not String.IsNullOrEmpty(BaseType) Then
                lDecl &= " Inherits " & BaseType
            End If
            
            Return lDecl
        End Function
        
        Private Function GetInterfaceDeclaration() As String
            Return GetVisibilityString() & " Interface " & Name
        End Function
        
        Private Function GetFieldDeclaration() As String
            Dim lDecl As String = GetVisibilityString() & " "
            
            If IsShared Then lDecl &= "Shared "
            If IsReadOnly Then lDecl &= "ReadOnly "
            If IsConst Then lDecl &= "Const "
            
            lDecl &= Name
            
            If Not String.IsNullOrEmpty(ReturnType) Then
                lDecl &= " As " & ReturnType
            End If
            
            Return lDecl
        End Function
        
        Private Function GetEventDeclaration() As String
            Dim lDecl As String = GetVisibilityString() & " "
            
            If IsShared Then lDecl &= "Shared "
            
            lDecl &= "Event " & Name
            
            If Parameters IsNot Nothing AndAlso Parameters.Count > 0 Then
                lDecl &= "("
                Dim lParams As New List(Of String)()
                for each lParam in Parameters
                    Dim lParamStr As String = lParam.Name
                    If Not String.IsNullOrEmpty(lParam.ParameterType) Then
                        lParamStr &= " As " & lParam.ParameterType
                    End If
                    lParams.Add(lParamStr)
                Next
                lDecl &= String.Join(", ", lParams)
                lDecl &= ")"
            End If
            
            Return lDecl
        End Function
        
        Private Function GetVisibilityString() As String
            Select Case Visibility
                Case eVisibility.ePublic
                    Return "Public"
                Case eVisibility.ePrivate
                    Return "Private"
                Case eVisibility.eProtected
                    Return "Protected"
                Case eVisibility.eFriend
                    Return "Friend"
                Case eVisibility.eProtectedFriend
                    Return "Protected Friend"
                Case Else
                    Return "Public"
            End Select
        End Function

    End Class
       
    ' Parameter information class
    Public Class ParameterInfo
        Public Property Name As String
        Public Property ParameterType As String
        Public Property IsOptional As Boolean = False
        Public Property DefaultValue As String = ""
        Public Property IsByRef As Boolean = False
        Public Property IsByVal As Boolean = True
        Public Property IsParamArray As Boolean = False
        
        Public Sub New()
        End Sub
        
        Public Sub New(vName As String, vType As String)
            Name = vName
            ParameterType = vType
        End Sub
        
        Public Function GetDisplayText() As String
            Dim lText As String = ""
            
            If IsByRef Then
                lText &= "ByRef "
            ElseIf IsParamArray Then
                lText &= "ParamArray "
            End If
            
            lText &= Name
            
            If Not String.IsNullOrEmpty(ParameterType) Then
                lText &= " As " & ParameterType
            End If
            
            If IsOptional AndAlso Not String.IsNullOrEmpty(DefaultValue) Then
                lText &= " = " & DefaultValue
            End If
            
            Return lText
        End Function
        
        Public Function Clone() As ParameterInfo
            Dim lClone As New ParameterInfo()
            lClone.Name = Name
            lClone.ParameterType = ParameterType
            lClone.IsOptional = IsOptional
            lClone.DefaultValue = DefaultValue
            lClone.IsByRef = IsByRef
            lClone.IsByVal = IsByVal
            lClone.IsParamArray = IsParamArray
            Return lClone
        End Function



    End Class
        
    
End Namespace
