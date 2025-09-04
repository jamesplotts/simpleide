' ProjectParser.vb - Parses entire project structure into unified SyntaxNode tree
' Created: 2025-08-25

Imports System
Imports System.IO
Imports System.Collections.Generic
Imports System.Text.RegularExpressions
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax
Imports SimpleIDE.Utilities

Namespace Managers
    
    ''' <summary>
    ''' Parses an entire VB.NET project into a unified SyntaxNode tree structure
    ''' </summary>
    Partial Public Class ProjectParser
        


        ''' <summary>
        ''' Parses delegate declarations with XML documentation support
        ''' </summary>
        Private Function ParseDelegate(vLine As String, vTypeNode As SyntaxNode, vLineNumber As Integer, vLines As List(Of String)) As Boolean
            Try
                Dim lTokens As List(Of String) = TokenizeLine(vLine)
                
                ' Find "Delegate" keyword
                Dim lDelegateIndex As Integer = -1
                for i As Integer = 0 To lTokens.Count - 1
                    If String.Equals(lTokens(i), "Delegate", StringComparison.OrdinalIgnoreCase) Then
                        lDelegateIndex = i
                        Exit for
                    End If
                Next
                
                If lDelegateIndex < 0 Then Return False
                
                ' Determine if it's a Sub or Function delegate
                Dim lIsSub As Boolean = False
                Dim lIsFunction As Boolean = False
                Dim lNameIndex As Integer = -1
                
                for i As Integer = lDelegateIndex + 1 To lTokens.Count - 1
                    If String.Equals(lTokens(i), "Sub", StringComparison.OrdinalIgnoreCase) Then
                        lIsSub = True
                        lNameIndex = i + 1
                        Exit for
                    ElseIf String.Equals(lTokens(i), "Function", StringComparison.OrdinalIgnoreCase) Then
                        lIsFunction = True
                        lNameIndex = i + 1
                        Exit for
                    End If
                Next
                
                If lNameIndex < 0 OrElse lNameIndex >= lTokens.Count Then Return False
                
                ' Get delegate name
                Dim lName As String = lTokens(lNameIndex)
                
                ' Remove any parentheses from name
                Dim lParenIndex As Integer = lName.IndexOf("("c)
                If lParenIndex >= 0 Then
                    lName = lName.Substring(0, lParenIndex)
                End If
                
                ' Create delegate node
                Dim lDelegateNode As New SyntaxNode(CodeNodeType.eDelegate, lName)
                lDelegateNode.StartLine = vLineNumber
                lDelegateNode.FilePath = pCurrentFile
                
                ' Parse modifiers
                ParseModifiers(vLine, lDelegateNode)
                
                ' Parse parameters
                ParseParameters(vLine, lDelegateNode)
                
                ' Parse return type (for function delegates)
                If lIsFunction Then
                    ParseReturnType(vLine, lDelegateNode)
                End If
                
                ' Extract and apply XML documentation
                ExtractAndApplyXmlDocumentation(vLines, vLineNumber, lDelegateNode)
                
                ' Add to parent
                vTypeNode.AddChild(lDelegateNode)
                
              '  Console.WriteLine($"  Added Delegate: {lName} To {vTypeNode.Name}")
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseDelegate error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Parses const declarations
        ''' </summary>
        Private Function ParseConst(vLine As String, vTypeNode As SyntaxNode, vLineNumber As Integer) As Boolean
            Try
                Dim lPattern As String = "^\s*(?:(Public|Private|Protected|Friend)\s+)*Const\s+(\w+)"
                Dim lMatch As Match = Regex.Match(vLine, lPattern, RegexOptions.IgnoreCase)
                
                If Not lMatch.Success Then
                    Return False
                End If
                
                Dim lName As String = lMatch.Groups(2).Value
                
                ' Create const node
                Dim lConstNode As New SyntaxNode(CodeNodeType.eConst, lName)
                lConstNode.FilePath = pCurrentFile
                lConstNode.StartLine = vLineNumber
                lConstNode.IsConst = True
                
                ' Parse modifiers
                ParseModifiers(vLine, lConstNode)
                
                ' Check for duplicate
                If Not IsDuplicateMember(vTypeNode, lConstNode) Then
                    vTypeNode.AddChild(lConstNode)
                End If
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseConst error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Parses enum value declarations
        ''' </summary>
        Private Sub ParseEnumValue(vLine As String, vEnumNode As SyntaxNode, vLineNumber As Integer)
            Try
                ' Simple pattern for enum values
                Dim lPattern As String = "^\s*(\w+)\s*(=.*)?$"
                Dim lMatch As Match = Regex.Match(vLine, lPattern)
                
                If lMatch.Success AndAlso Not vLine.StartsWith("End ", StringComparison.OrdinalIgnoreCase) Then
                    Dim lName As String = lMatch.Groups(1).Value
                    
                    ' Create enum value node
                    Dim lEnumValueNode As New SyntaxNode(CodeNodeType.eEnumValue, lName)
                    lEnumValueNode.FilePath = pCurrentFile
                    lEnumValueNode.StartLine = vLineNumber
                    
                    vEnumNode.AddChild(lEnumValueNode)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseEnumValue error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Private Methods - Helpers =====
        
        ''' <summary>
        ''' Parses modifiers from a declaration line and updates the node
        ''' </summary>
        Private Sub ParseModifiers(vLine As String, vNode As SyntaxNode)
            Try
                vNode.IsPublic = Regex.IsMatch(vLine, "\bPublic\b", RegexOptions.IgnoreCase)
                vNode.IsPrivate = Regex.IsMatch(vLine, "\bPrivate\b", RegexOptions.IgnoreCase)
                vNode.IsProtected = Regex.IsMatch(vLine, "\bProtected\b", RegexOptions.IgnoreCase)
                vNode.IsFriend = Regex.IsMatch(vLine, "\bFriend\b", RegexOptions.IgnoreCase)
                vNode.IsShared = Regex.IsMatch(vLine, "\bShared\b", RegexOptions.IgnoreCase)
                vNode.IsOverridable = Regex.IsMatch(vLine, "\bOverridable\b", RegexOptions.IgnoreCase)
                vNode.IsOverrides = Regex.IsMatch(vLine, "\bOverrides\b", RegexOptions.IgnoreCase)
                vNode.IsMustOverride = Regex.IsMatch(vLine, "\bMustOverride\b", RegexOptions.IgnoreCase)
                vNode.IsNotOverridable = Regex.IsMatch(vLine, "\bNotOverridable\b", RegexOptions.IgnoreCase)
                vNode.IsMustInherit = Regex.IsMatch(vLine, "\bMustInherit\b", RegexOptions.IgnoreCase)
                vNode.IsNotInheritable = Regex.IsMatch(vLine, "\bNotInheritable\b", RegexOptions.IgnoreCase)
                vNode.IsReadOnly = Regex.IsMatch(vLine, "\bReadOnly\b", RegexOptions.IgnoreCase)
                vNode.IsWriteOnly = Regex.IsMatch(vLine, "\bWriteOnly\b", RegexOptions.IgnoreCase)
                vNode.IsWithEvents = Regex.IsMatch(vLine, "\bWithEvents\b", RegexOptions.IgnoreCase)
                vNode.IsShadows = Regex.IsMatch(vLine, "\bShadows\b", RegexOptions.IgnoreCase)
                vNode.IsAsync = Regex.IsMatch(vLine, "\bAsync\b", RegexOptions.IgnoreCase)
                
                ' Set visibility based on modifiers
                If vNode.IsPublic Then
                    vNode.Visibility = SyntaxNode.eVisibility.ePublic
                ElseIf vNode.IsPrivate Then
                    vNode.Visibility = SyntaxNode.eVisibility.ePrivate
                ElseIf vNode.IsProtected AndAlso vNode.IsFriend Then
                    vNode.Visibility = SyntaxNode.eVisibility.eProtectedFriend
                ElseIf vNode.IsProtected Then
                    vNode.Visibility = SyntaxNode.eVisibility.eProtected
                ElseIf vNode.IsFriend Then
                    vNode.Visibility = SyntaxNode.eVisibility.eFriend
                Else
                    vNode.Visibility = SyntaxNode.eVisibility.ePublic ' Default to public
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseModifiers error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Parses type modifiers specifically for classes, modules, etc.
        ''' </summary>
        Private Sub ParseTypeModifiers(vLine As String, vNode As SyntaxNode)
            Try
                ParseModifiers(vLine, vNode)
                vNode.IsPartial = Regex.IsMatch(vLine, "\bPartial\b", RegexOptions.IgnoreCase)
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseTypeModifiers error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Parses inheritance and implementation clauses
        ''' </summary>
        Private Sub ParseInheritanceAndImplementation(vLine As String, vNode As SyntaxNode)
            Try
                ' Parse Inherits clause
                Dim lInheritsMatch As Match = Regex.Match(vLine, "\bInherits\s+(\S+)", RegexOptions.IgnoreCase)
                If lInheritsMatch.Success Then
                    vNode.BaseType = lInheritsMatch.Groups(1).Value
                    vNode.InheritsList.Add(lInheritsMatch.Groups(1).Value)
                End If
                
                ' Parse Implements clause
                Dim lImplementsMatch As Match = Regex.Match(vLine, "\bImplements\s+(.+)$", RegexOptions.IgnoreCase)
                If lImplementsMatch.Success Then
                    Dim lInterfaces As String() = lImplementsMatch.Groups(1).Value.Split(","c)
                    for each lInterface in lInterfaces
                        vNode.ImplementsList.Add(lInterface.Trim())
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseInheritanceAndImplementation error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Parses parameters from a method/function declaration
        ''' </summary>
        Private Sub ParseParameters(vLine As String, vNode As SyntaxNode)
            Try
                ' Extract parameters between parentheses
                Dim lParenStart As Integer = vLine.IndexOf("("c)
                Dim lParenEnd As Integer = vLine.LastIndexOf(")"c)
                
                If lParenStart >= 0 AndAlso lParenEnd > lParenStart Then
                    Dim lParamsString As String = vLine.Substring(lParenStart + 1, lParenEnd - lParenStart - 1)
                    
                    If Not String.IsNullOrWhiteSpace(lParamsString) Then
                        ' Split parameters by comma (simplified - doesn't handle nested generics)
                        Dim lParams As String() = lParamsString.Split(","c)
                        
                        for each lParam in lParams
                            Dim lTrimmedParam As String = lParam.Trim()
                            If Not String.IsNullOrEmpty(lTrimmedParam) Then
                                ' Parse parameter (simplified)
                                Dim lParamInfo As New ParameterInfo()
                                
                                ' Extract parameter name and type
                                Dim lParamMatch As Match = Regex.Match(lTrimmedParam, 
                                    "(?:(ByVal|ByRef|Optional|ParamArray)\s+)*(\w+)(?:\s+As\s+(.+))?", 
                                    RegexOptions.IgnoreCase)
                                
                                If lParamMatch.Success Then
                                    lParamInfo.Name = lParamMatch.Groups(2).Value
                                    If lParamMatch.Groups(3).Success Then
                                        ' FIXED: Use ParameterType instead of Type
                                        lParamInfo.ParameterType = lParamMatch.Groups(3).Value.Trim()
                                    End If
                                    lParamInfo.IsByRef = lTrimmedParam.IndexOf("ByRef", StringComparison.OrdinalIgnoreCase) >= 0
                                    lParamInfo.IsOptional = lTrimmedParam.IndexOf("Optional", StringComparison.OrdinalIgnoreCase) >= 0
                                    lParamInfo.IsParamArray = lTrimmedParam.IndexOf("ParamArray", StringComparison.OrdinalIgnoreCase) >= 0
                                    
                                    vNode.Parameters.Add(lParamInfo)
                                End If
                            End If
                        Next
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseParameters error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Parses the return type from a function or property declaration
        ''' </summary>
        Private Sub ParseReturnType(vLine As String, vNode As SyntaxNode)
            Try
                ' Look for "As TypeName" pattern after closing parenthesis or property name
                Dim lTypeMatch As Match = Regex.Match(vLine, "\)\s+As\s+(.+)$|\bProperty\s+\w+(?:\([^)]*\))?\s+As\s+(.+)$", RegexOptions.IgnoreCase)
                
                If lTypeMatch.Success Then
                    If lTypeMatch.Groups(1).Success Then
                        vNode.ReturnType = lTypeMatch.Groups(1).Value.Trim()
                    ElseIf lTypeMatch.Groups(2).Success Then
                        vNode.ReturnType = lTypeMatch.Groups(2).Value.Trim()
                    End If
                    
                    ' Clean up return type (remove trailing comments, etc.)
                    Dim lCommentIndex As Integer = vNode.ReturnType.IndexOf("'"c)
                    If lCommentIndex >= 0 Then
                        vNode.ReturnType = vNode.ReturnType.Substring(0, lCommentIndex).Trim()
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseReturnType error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Checks if a line is an End statement for a type using tokenization
        ''' </summary>
        Private Function IsEndTypeStatement(vLine As String) As Boolean
            Try
                Dim lTokens As List(Of String) = TokenizeLine(vLine)
                
                If lTokens.Count < 2 Then Return False
                
                ' Must start with "End"
                If Not lTokens(0).Equals("End", StringComparison.OrdinalIgnoreCase) Then
                    Return False
                End If
                
                ' Second token must be a type keyword
                Dim lTypeKeywords As String() = {"Class", "Module", "Interface", "Structure", "Enum"}
                for each lKeyword in lTypeKeywords
                    If lTokens(1).Equals(lKeyword, StringComparison.OrdinalIgnoreCase) Then
                        Return True
                    End If
                Next
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.IsEndTypeStatement error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Checks if a member would be a duplicate in the type (for partial classes)
        ''' </summary>
        Private Function IsDuplicateMember(vTypeNode As SyntaxNode, vMemberNode As SyntaxNode) As Boolean
            Try
                ' For partial classes, check for exact duplicates
                If Not vTypeNode.IsPartial Then
                    Return False
                End If
                
                for each lExistingMember in vTypeNode.Children
                    ' Same name and type
                    If String.Equals(lExistingMember.Name, vMemberNode.Name, StringComparison.OrdinalIgnoreCase) AndAlso
                       lExistingMember.NodeType = vMemberNode.NodeType Then
                        
                        ' For methods/functions, check parameter count (simplified overload check)
                        If vMemberNode.NodeType = CodeNodeType.eMethod OrElse
                           vMemberNode.NodeType = CodeNodeType.eFunction OrElse
                           vMemberNode.NodeType = CodeNodeType.eConstructor Then
                            
                            ' If parameter counts differ, it's an overload, not a duplicate
                            If lExistingMember.Parameters.Count <> vMemberNode.Parameters.Count Then
                                Return False
                            End If
                        End If
                        
                        ' It's a duplicate
                        LogError($"Duplicate member '{vMemberNode.Name}' in partial class '{vTypeNode.Name}'")
                        Return True
                    End If
                Next
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.IsDuplicateMember error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Validates the parsed structure
        ''' </summary>
        Private Sub ValidateStructure()
            Try
                ' Check for duplicate namespaces at same level
                ValidateNamespaces(pRootNode)
                
                ' Verify all partial classes are properly merged
                for each lKvp in pPartialClasses
                    Dim lNode As SyntaxNode = lKvp.Value
                    If lNode.Attributes.ContainsKey("FilePaths") Then
                        Dim lFileCount As Integer = lNode.Attributes("FilePaths").Split(";"c).Length
                        'Console.WriteLine($"Partial Class '{lNode.Name}' merged from {lFileCount} files")
                    End If
                Next
                
                ' Update end lines for all nodes
                UpdateEndLines(pRootNode)
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ValidateStructure error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Validates namespace structure for duplicates
        ''' </summary>
        Private Sub ValidateNamespaces(vNode As SyntaxNode)
            Try
                If vNode Is Nothing Then Return
                
                ' Check children for duplicate namespaces
                Dim lNamespaceNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                
                for each lChild in vNode.Children
                    If lChild.NodeType = CodeNodeType.eNamespace Then
                        If lNamespaceNames.Contains(lChild.Name) Then
                            LogError($"Duplicate Namespace '{lChild.Name}' at level")
                        Else
                            lNamespaceNames.Add(lChild.Name)
                        End If
                        
                        ' Recurse into child namespaces
                        ValidateNamespaces(lChild)
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ValidateNamespaces error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Updates end lines for all nodes based on their children
        ''' </summary>
        Private Sub UpdateEndLines(vNode As SyntaxNode)
            Try
                If vNode Is Nothing Then Return
                
                ' Process children first
                for each lChild in vNode.Children
                    UpdateEndLines(lChild)
                Next
                
                ' Update this node's end line based on children
                If vNode.Children.Count > 0 Then
                    Dim lMaxEndLine As Integer = vNode.StartLine
                    for each lChild in vNode.Children
                        If lChild.EndLine > lMaxEndLine Then
                            lMaxEndLine = lChild.EndLine
                        End If
                    Next
                    
                    ' Add a buffer for closing statements
                    vNode.EndLine = lMaxEndLine + 1
                Else
                    ' No children, end line is start line plus a small buffer
                    vNode.EndLine = vNode.StartLine + 1
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.UpdateEndLines error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Counts total nodes in the tree
        ''' </summary>
        Private Function CountNodes(vNode As SyntaxNode) As Integer
            Try
                If vNode Is Nothing Then Return 0
                
                Dim lCount As Integer = 1
                for each lChild in vNode.Children
                    lCount += CountNodes(lChild)
                Next
                
                Return lCount
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.CountNodes error: {ex.Message}")
                Return 0
            End Try
        End Function
        
        ''' <summary>
        ''' Logs an error message
        ''' </summary>
        Private Sub LogError(vMessage As String)
            Dim lErrorMessage As String = $"[{pCurrentFile}:{pCurrentLineNumber}] {vMessage}"
            pParseErrors.Add(lErrorMessage)
            Console.WriteLine($"ProjectParser error: {lErrorMessage}")
        End Sub


        
        ''' <summary>
        ''' Validates if a token is valid and should be included
        ''' </summary>
        ''' <param name="vToken">Token to validate</param>
        ''' <returns>True if valid, False otherwise</returns>
        Private Function IsValidToken(vToken As String) As Boolean
            Try
                ' Filter out invalid tokens
                If String.IsNullOrWhiteSpace(vToken) Then Return False
                
                ' Filter out standalone punctuation that shouldn't be tokens
                If vToken = ")" OrElse vToken = "(" OrElse vToken = "[" OrElse vToken = "]" OrElse
                   vToken = "{" OrElse vToken = "}" Then
                    Return False
                End If
                
                ' Filter out the word "all" if it appears in weird contexts
                ' (it should only appear as part of valid constructs like "for All")
                If vToken.ToLower() = "all" Then
                    ' This might be too aggressive - adjust if needed
                    Return False
                End If
                
                ' Valid token
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"IsValidToken error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Applies a modifier keyword to a syntax node
        ''' </summary>
        Private Sub ApplyModifierToNode(vModifier As String, vNode As SyntaxNode)
            Select Case vModifier.ToLower()
                Case "Public"
                    vNode.IsPublic = True
                    vNode.Visibility = SyntaxNode.eVisibility.ePublic
                Case "Private"
                    vNode.IsPrivate = True
                    vNode.Visibility = SyntaxNode.eVisibility.ePrivate
                Case "Protected"
                    vNode.IsProtected = True
                    vNode.Visibility = SyntaxNode.eVisibility.eProtected
                Case "Friend"
                    vNode.IsFriend = True
                    vNode.Visibility = SyntaxNode.eVisibility.eFriend
                Case "Partial"
                    vNode.IsPartial = True
                Case "Shared"
                    vNode.IsShared = True
                Case "MustInherit"
                    vNode.IsMustInherit = True
                Case "NotInheritable"
                    vNode.IsNotInheritable = True
                Case "Shadows"
                    vNode.IsShadows = True
                Case "Overrides"
                    vNode.IsOverrides = True
                Case "Overridable"
                    vNode.IsOverridable = True
                Case "MustOverride"
                    vNode.IsMustOverride = True
                Case "NotOverridable"
                    vNode.IsNotOverridable = True
                Case "ReadOnly"
                    vNode.IsReadOnly = True
                Case "WriteOnly"
                    vNode.IsWriteOnly = True
                Case "WithEvents"
                    vNode.IsWithEvents = True
                Case "async"
                    vNode.IsAsync = True
            End Select
        End Sub

        ''' <summary>
        ''' Determines if a token is a VB.NET modifier keyword
        ''' </summary>
        ''' <param name="vToken">The token to check</param>
        ''' <returns>True if the token is a modifier keyword</returns>
        Private Function IsModifier(vToken As String) As Boolean
            Try
                If String.IsNullOrEmpty(vToken) Then Return False
                
                Dim lModifiers As String() = {
                    "Public", "Private", "Protected", "Friend",
                    "Shared", "Static", "Partial", "MustInherit", 
                    "NotInheritable", "Overridable", "NotOverridable",
                    "MustOverride", "Overrides", "Shadows", 
                    "ReadOnly", "WriteOnly", "Const", "WithEvents",
                    "Default", "Async", "Iterator"
                }
                
                for each lModifier in lModifiers
                    If String.Equals(vToken, lModifier, StringComparison.OrdinalIgnoreCase) Then
                        Return True
                    End If
                Next
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.IsModifier error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Parses inheritance and implementation from tokenized line
        ''' </summary>
        Private Sub ParseInheritanceFromTokens(vTokens As List(Of String), vStartIndex As Integer, vNode As SyntaxNode)
            Try
                If vStartIndex >= vTokens.Count Then Return
                
                Dim lInInherits As Boolean = False
                Dim lInImplements As Boolean = False
                Dim lCurrentName As New System.Text.StringBuilder()
                
                for i As Integer = vStartIndex To vTokens.Count - 1
                    Dim lToken As String = vTokens(i)
                    
                    If lToken.Equals("Inherits", StringComparison.OrdinalIgnoreCase) Then
                        lInInherits = True
                        lInImplements = False
                        If lCurrentName.Length > 0 Then
                            lCurrentName.Clear()
                        End If
                    ElseIf lToken.Equals("Implements", StringComparison.OrdinalIgnoreCase) Then
                        lInImplements = True
                        lInInherits = False
                        If lCurrentName.Length > 0 Then
                            lCurrentName.Clear()
                        End If
                    ElseIf lToken = "," Then
                        ' Comma separates multiple interfaces
                        If lCurrentName.Length > 0 Then
                            If lInImplements Then
                                vNode.ImplementsList.Add(lCurrentName.ToString())
                            End If
                            lCurrentName.Clear()
                        End If
                    ElseIf lToken.Length = 1 AndAlso "(){}[]<>=+-/*:".Contains(lToken) Then
                        ' Skip operators
                        Continue for
                    Else
                        ' Build up the name
                        If lCurrentName.Length > 0 AndAlso Not lToken.StartsWith(".") Then
                            lCurrentName.Append(".")
                        End If
                        lCurrentName.Append(lToken)
                        
                        If lInInherits AndAlso String.IsNullOrEmpty(vNode.BaseType) Then
                            vNode.BaseType = lCurrentName.ToString()
                            vNode.InheritsList.Add(lCurrentName.ToString())
                        End If
                    End If
                Next
                
                ' Add final implementation if any
                If lInImplements AndAlso lCurrentName.Length > 0 Then
                    vNode.ImplementsList.Add(lCurrentName.ToString())
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseInheritanceFromTokens error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Parses the type information for a field or constant
        ''' </summary>
        Private Sub ParseFieldType(vLine As String, vNode As SyntaxNode)
            Try
                ' Look for "As TypeName" pattern
                Dim lAsMatch As Match = Regex.Match(vLine, "\bAs\s+([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*(?:\([^)]*\))?)", RegexOptions.IgnoreCase)
                
                If lAsMatch.Success Then
                    vNode.ReturnType = lAsMatch.Groups(1).Value.Trim()
                    
                    ' Clean up the type (remove trailing comments, etc.)
                    Dim lCommentIndex As Integer = vNode.ReturnType.IndexOf("'"c)
                    If lCommentIndex >= 0 Then
                        vNode.ReturnType = vNode.ReturnType.Substring(0, lCommentIndex).Trim()
                    End If
                    
                    Dim lEqualsIndex As Integer = vNode.ReturnType.IndexOf("="c)
                    If lEqualsIndex >= 0 Then
                        vNode.ReturnType = vNode.ReturnType.Substring(0, lEqualsIndex).Trim()
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseFieldType error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Parses constant declarations with XML documentation support
        ''' </summary>
        Private Function ParseConstant(vLine As String, vTypeNode As SyntaxNode, vLineNumber As Integer, vLines As List(Of String)) As Boolean
            Try
                Dim lTokens As List(Of String) = TokenizeLine(vLine)
                
                ' Find "Const" keyword
                Dim lConstIndex As Integer = -1
                for i As Integer = 0 To lTokens.Count - 1
                    If String.Equals(lTokens(i), "Const", StringComparison.OrdinalIgnoreCase) Then
                        lConstIndex = i
                        Exit for
                    End If
                Next
                
                If lConstIndex < 0 OrElse lConstIndex >= lTokens.Count - 1 Then
                    Return False
                End If
                
                ' Get constant name
                Dim lName As String = lTokens(lConstIndex + 1)
                
                ' Remove any type declaration or assignment from name
                Dim lAsIndex As Integer = lName.IndexOf(" As ")
                If lAsIndex >= 0 Then
                    lName = lName.Substring(0, lAsIndex)
                End If
                
                Dim lEqualsIndex As Integer = lName.IndexOf("="c)
                If lEqualsIndex >= 0 Then
                    lName = lName.Substring(0, lEqualsIndex)
                End If
                
                lName = lName.Trim()
                
                ' Create constant node
                Dim lConstNode As New SyntaxNode(CodeNodeType.eConst, lName)
                lConstNode.StartLine = vLineNumber
                lConstNode.FilePath = pCurrentFile
                
                ' Parse modifiers
                ParseModifiers(vLine, lConstNode)
                
                ' Parse type if specified
                ParseFieldType(vLine, lConstNode)
                
                ' Extract initial value if present
                Dim lValueMatch As Match = Regex.Match(vLine, "=\s*(.+?)(\s*'|$)")
                If lValueMatch.Success Then
                    lConstNode.InitialValue = lValueMatch.Groups(1).Value.Trim()
                End If
                
                ' Extract and apply XML documentation
                ExtractAndApplyXmlDocumentation(vLines, vLineNumber, lConstNode)
                
                ' Add to parent
                vTypeNode.AddChild(lConstNode)
                
             '   Console.WriteLine($"  Added Const: {lName} To {vTypeNode.Name}")
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseConstant error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Parses the body of a type declaration (Class, Module, Interface, etc.)
        ''' </summary>
        ''' <param name="vLines">All lines in the file</param>
        ''' <param name="vStartLine">Starting line index of the type body</param>
        ''' <param name="vTypeNode">The type node to populate with members</param>
        ''' <returns>The line index after the type body ends</returns>
        Private Function ParseTypeBody(vLines As List(Of String), vStartLine As Integer, vTypeNode As SyntaxNode) As Integer
            Try
                Dim i As Integer = vStartLine
                Dim lNestLevel As Integer = 1
                
                While i < vLines.Count AndAlso lNestLevel > 0
                    Dim lLine As String = vLines(i).Trim()
                    
                    ' Skip empty lines and comments
                    If String.IsNullOrWhiteSpace(lLine) OrElse lLine.StartsWith("'") Then
                        i += 1
                        Continue While
                    End If
                    
                    ' Check for End statement
                    If IsEndTypeStatement(lLine) Then
                        lNestLevel -= 1
                        If lNestLevel = 0 Then
                            vTypeNode.EndLine = i
                            Return i
                        End If
                    End If
                    
                    ' Parse members within the type
                    If lNestLevel = 1 Then
                        ' Try to parse as method/function
                        If ParseMethodOrFunction(lLine, vTypeNode, i) Then
                            ' Skip to end of method
                            i = SkipToEndOfMethod(vLines, i + 1)
                        ' Try to parse as property
                        ElseIf ParseProperty(lLine, vTypeNode, i) Then
                            ' Skip to end of property if multi-line
                            If Not lLine.Contains("=") AndAlso Not lLine.Contains("Get") AndAlso Not lLine.Contains("Set") Then
                                i = SkipToEndOfProperty(vLines, i + 1)
                            End If
                        ' Try to parse as field
                        ElseIf ParseField(lLine, vTypeNode, i, vLines) Then
                            ' Single line - nothing more to do
                        ' Try to parse as event
                        ElseIf ParseEvent(lLine, vTypeNode, i, vLines) Then
                            ' Single line - nothing more to do
                        End If
                    End If
                    
                    i += 1
                End While
                
                Return i
                
            Catch ex As Exception
                Console.WriteLine($"ParseTypeBody error: {ex.Message}")
                Return vStartLine + 1
            End Try
        End Function
        
        ''' <summary>
        ''' Skips lines until the end of a method is found
        ''' </summary>
        Private Function SkipToEndOfMethod(vLines As List(Of String), vStartLine As Integer) As Integer
            Try
                Dim i As Integer = vStartLine
                Dim lNestLevel As Integer = 1
                
                While i < vLines.Count AndAlso lNestLevel > 0
                    Dim lLine As String = vLines(i).Trim()
                    
                    ' Check for nested blocks
                    If Regex.IsMatch(lLine, "\b(If|for|While|Do|Select|Try|Using|SyncLock|with)\b", RegexOptions.IgnoreCase) Then
                        ' Check if it's not a single-line If
                        If Not Regex.IsMatch(lLine, "\bThen\b.+\bEnd If\b", RegexOptions.IgnoreCase) Then
                            lNestLevel += 1
                        End If
                    ElseIf Regex.IsMatch(lLine, "^\s*End\s+(If|for|While|Do|Select|Try|Using|SyncLock|with)\b", RegexOptions.IgnoreCase) Then
                        lNestLevel -= 1
                    ElseIf Regex.IsMatch(lLine, "^\s*End\s+(Sub|Function)\b", RegexOptions.IgnoreCase) Then
                        lNestLevel -= 1
                        If lNestLevel = 0 Then
                            Return i
                        End If
                    End If
                    
                    i += 1
                End While
                
                Return i
                
            Catch ex As Exception
                Console.WriteLine($"SkipToEndOfMethod error: {ex.Message}")
                Return vStartLine
            End Try
        End Function
        
        ''' <summary>
        ''' Skips lines until the end of a property is found
        ''' </summary>
        Private Function SkipToEndOfProperty(vLines As List(Of String), vStartLine As Integer) As Integer
            Try
                Dim i As Integer = vStartLine
                
                While i < vLines.Count
                    Dim lLine As String = vLines(i).Trim()
                    
                    If Regex.IsMatch(lLine, "^\s*End\s+Property\b", RegexOptions.IgnoreCase) Then
                        Return i
                    End If
                    
                    i += 1
                End While
                
                Return i
                
            Catch ex As Exception
                Console.WriteLine($"SkipToEndOfProperty error: {ex.Message}")
                Return vStartLine
            End Try
        End Function
        
        ''' <summary>
        ''' Merges members from a partial class into an existing class node
        ''' </summary>
        ''' <param name="vExistingClass">The existing class to merge into</param>
        ''' <param name="vNewClass">The new partial class with members to merge</param>
        ''' <remarks>
        ''' Fixed to ensure all members are properly merged without loss
        ''' </remarks>
        Private Sub MergePartialClass(vExistingClass As SyntaxNode, vNewClass As SyntaxNode)
            Try
                If vExistingClass Is Nothing OrElse vNewClass Is Nothing Then Return
                
                Console.WriteLine($"MergePartialClass: Merging {vNewClass.Children.Count} members from partial class {vNewClass.Name}")
                Console.WriteLine($"  Existing class has {vExistingClass.Children.Count} members before merge")
                
                ' Mark both as partial
                vExistingClass.IsPartial = True
                vNewClass.IsPartial = True
                
                ' Initialize attributes if needed
                If vExistingClass.Attributes Is Nothing Then
                    vExistingClass.Attributes = New Dictionary(Of String, String)()
                End If
                
                ' Merge file paths
                If vExistingClass.Attributes.ContainsKey("FilePaths") Then
                    Dim lExistingPaths As String = vExistingClass.Attributes("FilePaths")
                    If Not String.IsNullOrEmpty(vNewClass.FilePath) AndAlso Not lExistingPaths.Contains(vNewClass.FilePath) Then
                        vExistingClass.Attributes("FilePaths") = lExistingPaths & ";" & vNewClass.FilePath
                    End If
                Else
                    Dim lPaths As String = If(Not String.IsNullOrEmpty(vExistingClass.FilePath), vExistingClass.FilePath, "")
                    If Not String.IsNullOrEmpty(vNewClass.FilePath) AndAlso vNewClass.FilePath <> lPaths Then
                        If Not String.IsNullOrEmpty(lPaths) Then
                            lPaths &= ";" & vNewClass.FilePath
                        Else
                            lPaths = vNewClass.FilePath
                        End If
                    End If
                    If Not String.IsNullOrEmpty(lPaths) Then
                        vExistingClass.Attributes("FilePaths") = lPaths
                    End If
                End If
                
                ' Merge children (members)
                Dim lMergedCount As Integer = 0
                Dim lSkippedCount As Integer = 0
                
                for each lNewMember in vNewClass.Children
                    ' For partial classes, we want to add ALL members unless they are exact duplicates
                    ' (same name, same signature, same node type)
                    Dim lIsDuplicate As Boolean = False
                    
                    ' Only check for duplicates for specific node types where duplicates matter
                    Select Case lNewMember.NodeType
                        Case CodeNodeType.eMethod, CodeNodeType.eFunction, CodeNodeType.eConstructor
                            ' For methods, check name and signature
                            lIsDuplicate = HasDuplicateMethod(vExistingClass, lNewMember)
                            
                        Case CodeNodeType.eProperty
                            ' For properties, check exact name match
                            lIsDuplicate = HasDuplicateProperty(vExistingClass, lNewMember)
                            
                        Case CodeNodeType.eField, CodeNodeType.eConst
                            ' For fields, check exact name match
                            lIsDuplicate = HasDuplicateField(vExistingClass, lNewMember)
                            
                        Case CodeNodeType.eEvent
                            ' For events, check exact name match
                            lIsDuplicate = HasDuplicateEvent(vExistingClass, lNewMember)
                            
                        Case Else
                            ' For other types, allow all
                            lIsDuplicate = False
                    End Select
                    
                    If Not lIsDuplicate Then
                        ' Set the parent reference correctly
                        lNewMember.Parent = vExistingClass
                        vExistingClass.AddChild(lNewMember)
                        lMergedCount += 1
                        Console.WriteLine($"    Merged member: {lNewMember.Name} ({lNewMember.NodeType})")
                    Else
                        lSkippedCount += 1
                        Console.WriteLine($"    Skipped duplicate: {lNewMember.Name} ({lNewMember.NodeType})")
                    End If
                Next
                
                ' Update line ranges to encompass all partial definitions
                If vNewClass.StartLine < vExistingClass.StartLine Then
                    vExistingClass.StartLine = vNewClass.StartLine
                End If
                If vNewClass.EndLine > vExistingClass.EndLine Then
                    vExistingClass.EndLine = vNewClass.EndLine
                End If
                
                Console.WriteLine($"  Merge complete: Added {lMergedCount} members, skipped {lSkippedCount} duplicates")
                Console.WriteLine($"  Existing class now has {vExistingClass.Children.Count} total members")
                
            Catch ex As Exception
                Console.WriteLine($"MergePartialClass error: {ex.Message}")
                Console.WriteLine($"  Stack: {ex.StackTrace}")
            End Try
        End Sub


        ''' <summary>
        ''' Checks if a string is a valid VB.NET identifier
        ''' </summary>
        ''' <param name="vName">The name to check</param>
        ''' <returns>True if valid identifier, False otherwise</returns>
        Private Function IsValidIdentifier(vName As String) As Boolean
            Try
                If String.IsNullOrWhiteSpace(vName) Then Return False
                
                ' Filter out single punctuation characters
                If vName.Length = 1 Then
                    Dim lChar As Char = vName(0)
                    If lChar = ")"c OrElse lChar = "("c OrElse lChar = "["c OrElse lChar = "]"c OrElse
                       lChar = "{"c OrElse lChar = "}"c OrElse lChar = "."c OrElse lChar = ","c OrElse
                       lChar = ";"c OrElse lChar = ":"c OrElse lChar = "!"c OrElse lChar = "?"c Then
                        Return False
                    End If
                End If
                
                ' Filter out the word "all" as a standalone identifier (it's a contextual keyword)
                If String.Equals(vName, "all", StringComparison.OrdinalIgnoreCase) Then
                    Return False
                End If
                
                ' Must start with letter or underscore
                If Not (Char.IsLetter(vName(0)) OrElse vName(0) = "_"c) Then
                    Return False
                End If
                
                ' Rest must be letters, digits, or underscores
                for i As Integer = 1 To vName.Length - 1
                    If Not (Char.IsLetterOrDigit(vName(i)) OrElse vName(i) = "_"c) Then
                        Return False
                    End If
                Next
                
                ' Check it's not just underscores
                If vName.All(Function(c) c = "_"c) Then
                    Return False
                End If
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"IsValidIdentifier error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Information about string delimiters in a line
        ''' </summary>
        Private Structure StringDelimiterInfo
            Public HasOddQuotes As Boolean
            Public IsInterpolated As Boolean
            Public QuoteCount As Integer
        End Structure
        
        ''' <summary>
        ''' Checks if a line contains any string delimiter (regular or interpolated)
        ''' </summary>
        Private Function ContainsStringDelimiter(vLine As String) As Boolean
            ' Check for regular quotes
            If vLine.Contains("""") Then
                Return True
            End If
            
            ' Check for interpolated string start ($")
            If vLine.Contains("$""") Then
                Return True
            End If
            
            Return False
        End Function
        
        ''' <summary>
        ''' Analyzes string delimiters in a line, handling both regular and interpolated strings
        ''' </summary>
        Private Function AnalyzeStringDelimiters(vLine As String) As StringDelimiterInfo
            Try
                Dim lInfo As New StringDelimiterInfo()
                Dim lQuoteCount As Integer = 0
                Dim i As Integer = 0
                
                While i < vLine.Length
                    ' Check for interpolated string start ($")
                    If i < vLine.Length - 1 AndAlso vLine(i) = "$"c AndAlso vLine(i + 1) = """"c Then
                        lInfo.IsInterpolated = True
                        ' Count this as a quote
                        lQuoteCount += 1
                        i += 2 ' Skip both $ and "
                        Continue While
                    End If
                    
                    ' Check for regular quote
                    If vLine(i) = """"c Then
                        ' Check if it's an escaped quote ("")
                        If i < vLine.Length - 1 AndAlso vLine(i + 1) = """"c Then
                            ' Escaped quote - skip both, don't count
                            i += 2
                        Else
                            ' Real quote
                            lQuoteCount += 1
                            i += 1
                        End If
                    Else
                        i += 1
                    End If
                End While
                
                lInfo.QuoteCount = lQuoteCount
                lInfo.HasOddQuotes = (lQuoteCount Mod 2 = 1)
                
                Return lInfo
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.AnalyzeStringDelimiters error: {ex.Message}")
                Return New StringDelimiterInfo() with {.HasOddQuotes = False}
            End Try
        End Function

        ''' <summary>
        ''' Counts the number of quote marks in a line, properly handling escaped quotes and interpolated strings
        ''' </summary>
        ''' <param name="vLine">The line to count quotes in</param>
        ''' <returns>The number of unescaped quote marks</returns>
        Private Function CountQuotes(vLine As String) As Integer
            Try
                Dim lInfo As StringDelimiterInfo = AnalyzeStringDelimiters(vLine)
                Return lInfo.QuoteCount
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.CountQuotes error: {ex.Message}")
                Return 0
            End Try
        End Function

        ''' <summary>
        ''' Special handler for partial classes that need to be placed in specific namespaces
        ''' </summary>
        ''' <param name="vClassNode">The class node to process</param>
        ''' <param name="vDeclaredNamespace">The namespace declared in the file</param>
        ''' <param name="vRootNamespace">The project root namespace</param>
        Private Sub ProcessPartialClassInNamespace(vClassNode As SyntaxNode, vDeclaredNamespace As String, vRootNamespace As SyntaxNode)
            Try
                ' Special case: ProjectInfo is declared in Managers namespace but inherits from ProjectFileParser.ProjectInfo
                If vClassNode.Name = "ProjectInfo" AndAlso vDeclaredNamespace = "Managers" Then
                    ' Find or create the Managers namespace
                    Dim lManagersNamespace As SyntaxNode = Nothing
                    
                    ' Search for existing Managers namespace in root
                    for each lChild in vRootNamespace.Children
                        If lChild.NodeType = CodeNodeType.eNamespace AndAlso 
                           String.Equals(lChild.Name, "Managers", StringComparison.OrdinalIgnoreCase) Then
                            lManagersNamespace = lChild
                            Exit for
                        End If
                    Next
                    
                    ' Create Managers namespace if not found
                    If lManagersNamespace Is Nothing Then
                        lManagersNamespace = New SyntaxNode(CodeNodeType.eNamespace, "Managers")
                        lManagersNamespace.IsImplicit = False
                        vRootNamespace.AddChild(lManagersNamespace)
                      '  Console.WriteLine("Created Managers Namespace under root")
                    End If
                    
                    ' Move or merge the class into Managers namespace
                    Dim lExistingClass As SyntaxNode = Nothing
                    for each lChild in lManagersNamespace.Children
                        If lChild.NodeType = CodeNodeType.eClass AndAlso
                           String.Equals(lChild.Name, vClassNode.Name, StringComparison.OrdinalIgnoreCase) Then
                            lExistingClass = lChild
                            Exit for
                        End If
                    Next
                    
                    If lExistingClass IsNot Nothing Then
                        ' Merge with existing partial class
                        MergePartialClass(lExistingClass, vClassNode)
                        'Console.WriteLine($"Merged ProjectInfo into existing Class in Managers Namespace")
                    Else
                        ' Add new class to Managers namespace
                        lManagersNamespace.AddChild(vClassNode)
                       ' Console.WriteLine($"Added ProjectInfo To Managers Namespace")
                    End If
                    
                    ' Remove from root if it was incorrectly placed there
                    If vRootNamespace.Children.Contains(vClassNode) Then
                        vRootNamespace.Children.Remove(vClassNode)
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ProcessPartialClassInNamespace error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Generates line metadata with syntax tokens for a source file
        ''' </summary>
        ''' <param name="vSourceFile">The source file to parse</param>
        ''' <param name="vParseResult">The parse result to populate with metadata</param>
        Private Sub GenerateLineMetadata(vSourceFile As SourceFileInfo, vParseResult As Syntax.ParseResult)
            Try
                If vSourceFile Is Nothing OrElse vSourceFile.TextLines Is Nothing Then Return
                
                ' Initialize line metadata array
                Dim lLineCount As Integer = vSourceFile.TextLines.Count
                ReDim vParseResult.LineMetadata(Math.Max(0, lLineCount - 1))
                
                ' Create metadata for each line
                for i As Integer = 0 To lLineCount - 1
                    vParseResult.LineMetadata(i) = New LineMetadata()
                Next
                
                ' Parse each line for syntax tokens
                for lLineIndex As Integer = 0 To lLineCount - 1
                    Dim lLine As String = vSourceFile.TextLines(lLineIndex)
                    Dim lMetadata As LineMetadata = vParseResult.LineMetadata(lLineIndex)
                    
                    ' Generate syntax tokens for this line
                    GenerateSyntaxTokensForLine(lLine, lLineIndex, lMetadata)
                    
                    ' Update line hash for change detection
                    lMetadata.UpdateHash(lLine)
                Next
                
               ' Console.WriteLine($"Generated LineMetadata for {lLineCount} lines")
                
            Catch ex As Exception
                Console.WriteLine($"GenerateLineMetadata error: {ex.Message}")
            End Try
        End Sub        

        ''' <summary>
        ''' Generates syntax tokens for a single line
        ''' </summary>
        ''' <param name="vLineText">The text of the line</param>
        ''' <param name="vLineIndex">The line index</param>
        ''' <param name="vMetadata">The metadata to populate</param>
        Private Sub GenerateSyntaxTokensForLine(vLineText As String, vLineIndex As Integer, vMetadata As LineMetadata)
            Try
                vMetadata.SyntaxTokens.Clear()
                
                If String.IsNullOrEmpty(vLineText) Then Return
                
                Dim lPosition As Integer = 0
                Dim lInString As Boolean = False
                Dim lInComment As Boolean = False
                Dim lStringStart As Integer = 0
                Dim lTokenStart As Integer = 0
                Dim lCurrentToken As New System.Text.StringBuilder
                
                ' Check for line comment
                Dim lCommentIndex As Integer = vLineText.IndexOf("'"c)
                If lCommentIndex >= 0 Then
                    ' Check if it's not inside a string
                    Dim lQuoteCount As Integer = 0
                    For i As Integer = 0 To lCommentIndex - 1
                        If vLineText(i) = """"c Then lQuoteCount += 1
                    Next
                    
                    If lQuoteCount Mod 2 = 0 Then
                        ' Not in a string, this is a comment
                        If lCommentIndex > 0 Then
                            ' Parse the part before the comment
                            ParseLineSegment(vLineText.Substring(0, lCommentIndex), 0, vMetadata)
                        End If
                        
                        ' Add the comment token
                        vMetadata.SyntaxTokens.Add(New SyntaxToken(
                            lCommentIndex,
                            vLineText.Length,
                            SyntaxTokenType.eComment
                        ))
                        Return
                    End If
                End If
                
                ' Parse the entire line if no comment
                ParseLineSegment(vLineText, 0, vMetadata)
                
            Catch ex As Exception
                Console.WriteLine($"GenerateSyntaxTokensForLine error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Parses a segment of a line for syntax tokens
        ''' </summary>
        ''' <param name="vSegment">The text segment to parse</param>
        ''' <param name="vStartOffset">Starting offset in the line</param>
        ''' <param name="vMetadata">The metadata to populate</param>
        Private Sub ParseLineSegment(vSegment As String, vStartOffset As Integer, vMetadata As LineMetadata)
            Try
                Dim lTokenizer As New VBTokenizer()
                Dim lTokens As List(Of Token) = lTokenizer.TokenizeLine(vSegment)
                
                For Each lToken In lTokens
                    ' Map Token type to SyntaxTokenType
                    Dim lSyntaxType As SyntaxTokenType = MapTokenTypeToSyntaxType(lToken.Type)
                    
                    ' Calculate the token length
                    Dim lTokenLength As Integer = lToken.EndColumn - lToken.StartColumn + 1
                    
                    ' Create syntax token with proper constructor parameters
                    ' Constructor expects: (StartColumn, Length, TokenType, Color)
                    Dim lSyntaxToken As New SyntaxToken(
                        vStartOffset + lToken.StartColumn,
                        lTokenLength,
                        lSyntaxType
                    )
                    
                    vMetadata.SyntaxTokens.Add(lSyntaxToken)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ParseLineSegment error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Gets the color for a token type
        ''' </summary>
        ''' <param name="vTokenType">The syntax token type</param>
        ''' <returns>The color as a hex string</returns>
        Private Function GetColorForTokenType(vTokenType As SyntaxTokenType) As String
            ' These are default colors - they should match your theme
            Select Case vTokenType
                Case SyntaxTokenType.eKeyword
                    Return "#569CD6"  ' Blue for keywords
                Case SyntaxTokenType.eString
                    Return "#CE9178"  ' Orange for strings  
                Case SyntaxTokenType.eComment
                    Return "#6A9955"  ' Green for comments
                Case SyntaxTokenType.eNumber
                    Return "#B5CEA8"  ' Light green for numbers
                Case SyntaxTokenType.eType
                    Return "#4EC9B0"  ' Teal for types
                Case SyntaxTokenType.eIdentifier
                    Return "#D4D4D4"  ' Light gray for identifiers
                Case SyntaxTokenType.eOperator
                    Return "#D4D4D4"  ' Light gray for operators
                Case Else
                    Return "#D4D4D4"  ' Default light gray
            End Select
        End Function
        
        ''' <summary>
        ''' Maps a TokenType to a SyntaxTokenType
        ''' </summary>
        ''' <param name="vTokenType">The token type to map</param>
        ''' <returns>The corresponding syntax token type</returns>
        Private Function MapTokenTypeToSyntaxType(vTokenType As TokenType) As SyntaxTokenType
            Select Case vTokenType
                Case TokenType.eKeyword
                    Return SyntaxTokenType.eKeyword
                Case TokenType.eIdentifier
                    Return SyntaxTokenType.eIdentifier
                Case TokenType.eStringLiteral
                    Return SyntaxTokenType.eString
                Case TokenType.eNumber
                    Return SyntaxTokenType.eNumber
                Case TokenType.eComment
                    Return SyntaxTokenType.eComment
                Case TokenType.eOperator
                    Return SyntaxTokenType.eOperator
                Case TokenType.eType
                    Return SyntaxTokenType.eType
                Case Else
                    Return SyntaxTokenType.eNormal
            End Select
        End Function
        


        ''' <summary>
        ''' Gets the list of parse errors from the last parse operation
        ''' </summary>
        ''' <returns>List of error messages</returns>
        Public Function GetParseErrors() As List(Of String)
            Return If(pParseErrors, New List(Of String)())
        End Function

        ''' <summary>
        ''' Checks if a method with the same signature already exists
        ''' </summary>
        Private Function HasDuplicateMethod(vClass As SyntaxNode, vMethod As SyntaxNode) As Boolean
            Try
                For Each lChild In vClass.Children
                    If lChild.NodeType = vMethod.NodeType AndAlso 
                       String.Equals(lChild.Name, vMethod.Name, StringComparison.OrdinalIgnoreCase) Then
                        ' TODO: Could also check parameter signatures here for overloads
                        Return True
                    End If
                Next
                Return False
            Catch ex As Exception
                Console.WriteLine($"HasDuplicateMethod error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Checks if a property with the same name already exists
        ''' </summary>
        Private Function HasDuplicateProperty(vClass As SyntaxNode, vProperty As SyntaxNode) As Boolean
            Try
                For Each lChild In vClass.Children
                    If lChild.NodeType = CodeNodeType.eProperty AndAlso 
                       String.Equals(lChild.Name, vProperty.Name, StringComparison.OrdinalIgnoreCase) Then
                        Return True
                    End If
                Next
                Return False
            Catch ex As Exception
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Checks if a field with the same name already exists
        ''' </summary>
        Private Function HasDuplicateField(vClass As SyntaxNode, vField As SyntaxNode) As Boolean
            Try
                For Each lChild In vClass.Children
                    If (lChild.NodeType = CodeNodeType.eField OrElse lChild.NodeType = CodeNodeType.eConst) AndAlso 
                       String.Equals(lChild.Name, vField.Name, StringComparison.OrdinalIgnoreCase) Then
                        Return True
                    End If
                Next
                Return False
            Catch ex As Exception
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Checks if an event with the same name already exists
        ''' </summary>
        Private Function HasDuplicateEvent(vClass As SyntaxNode, vEvent As SyntaxNode) As Boolean
            Try
                For Each lChild In vClass.Children
                    If lChild.NodeType = CodeNodeType.eEvent AndAlso 
                       String.Equals(lChild.Name, vEvent.Name, StringComparison.OrdinalIgnoreCase) Then
                        Return True
                    End If
                Next
                Return False
            Catch ex As Exception
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Gets or creates a partial type node, properly handling merging
        ''' </summary>
        ''' <param name="vParentNode">The parent node (namespace or type)</param>
        ''' <param name="vTypeName">The name of the type</param>
        ''' <param name="vNodeType">The type of node (Class, Module, etc.)</param>
        ''' <param name="vFilePath">The file path where this partial is defined</param>
        ''' <returns>The type node (existing or newly created)</returns>
        ''' <remarks>
        ''' Fixed to properly handle partial class detection and merging
        ''' </remarks>
        Private Function GetOrCreatePartialTypeNode(vParentNode As SyntaxNode, vTypeName As String, 
                                                   vNodeType As CodeNodeType, vFilePath As String) As SyntaxNode
            Try
                If vParentNode Is Nothing OrElse String.IsNullOrEmpty(vTypeName) Then Return Nothing
                
                ' Create a unique key for this type based on parent path and type name
                Dim lParentPath As String = GetNodePath(vParentNode)
                Dim lKey As String = $"{lParentPath}:{vTypeName}"
                
                Console.WriteLine($"GetOrCreatePartialTypeNode: Looking for '{vTypeName}' in '{lParentPath}'")
                
                ' First, check if we already have this partial type in our tracking dictionary
                Dim lPartialNode As SyntaxNode = Nothing
                If pPartialClasses.TryGetValue(lKey, lPartialNode) Then
                    ' Found existing partial class in tracking dictionary
                    Console.WriteLine($"  Found existing partial in dictionary with {lPartialNode.Children.Count} members")
                    
                    ' Update the end line if this is a continuation
                    If pCurrentLineNumber > lPartialNode.EndLine Then
                        lPartialNode.EndLine = pCurrentLineNumber
                    End If
                    
                    ' Ensure it's marked as partial
                    lPartialNode.IsPartial = True
                    Return lPartialNode
                End If
                
                ' Check if a matching type already exists in the parent's children
                for each lChild in vParentNode.Children
                    If lChild.NodeType = vNodeType AndAlso
                       String.Equals(lChild.Name, vTypeName, StringComparison.OrdinalIgnoreCase) Then
                        ' Found existing node - this must be a partial class
                        Console.WriteLine($"  Found existing type in parent with {lChild.Children.Count} members, converting to partial")
                        
                        ' Mark as partial
                        lChild.IsPartial = True
                        
                        ' Add to partial tracking dictionary
                        pPartialClasses(lKey) = lChild
                        
                        ' Track the file path
                        If lChild.Attributes Is Nothing Then
                            lChild.Attributes = New Dictionary(Of String, String)()
                        End If
                        
                        If lChild.Attributes.ContainsKey("FilePaths") Then
                            Dim lPaths As String = lChild.Attributes("FilePaths")
                            If Not lPaths.Contains(vFilePath) Then
                                lChild.Attributes("FilePaths") = lPaths & ";" & vFilePath
                            End If
                        Else
                            lChild.Attributes("FilePaths") = vFilePath
                        End If
                        
                        Return lChild
                    End If
                Next
                
                ' No existing type found - create new partial type node
                Console.WriteLine($"  Creating new partial type '{vTypeName}'")
                
                Dim lNewNode As New SyntaxNode(vNodeType, vTypeName)
                lNewNode.FilePath = vFilePath
                lNewNode.StartLine = pCurrentLineNumber
                lNewNode.IsPartial = True
                
                ' Initialize attributes and track file path
                lNewNode.Attributes = New Dictionary(Of String, String)()
                lNewNode.Attributes("FilePaths") = vFilePath
                
                ' Add to parent
                vParentNode.AddChild(lNewNode)
                
                ' Add to partial tracking dictionary
                pPartialClasses(lKey) = lNewNode
                
                Console.WriteLine($"  Created New Partial type node for '{vTypeName}'")
                Return lNewNode
                
            Catch ex As Exception
                Console.WriteLine($"GetOrCreatePartialTypeNode error: {ex.Message}")
                Console.WriteLine($"  Stack: {ex.StackTrace}")
                Return Nothing
            End Try
        End Function

        ''' <summary>
        ''' Helper to get the full path of a node for unique identification
        ''' </summary>
        Private Function GetNodePath(vNode As SyntaxNode) As String
            Try
                If vNode Is Nothing Then Return ""
                
                Dim lPath As New List(Of String)()
                Dim lCurrent As SyntaxNode = vNode
                
                While lCurrent IsNot Nothing AndAlso lCurrent.NodeType <> CodeNodeType.eDocument
                    If Not String.IsNullOrEmpty(lCurrent.Name) Then
                        lPath.Insert(0, lCurrent.Name)
                    End If
                    lCurrent = lCurrent.Parent
                End While
                
                Return String.Join(".", lPath)
                
            Catch ex As Exception
                Console.WriteLine($"GetNodePath error: {ex.Message}")
                Return ""
            End Try
        End Function

        ''' <summary>
        ''' Gets or creates a namespace node in the project tree
        ''' </summary>
        ''' <param name="vNamespaceName">The name of the namespace (can be nested like "SimpleIDE.Models")</param>
        ''' <returns>The namespace node</returns>
        ''' <remarks>
        ''' This method handles nested namespaces by creating the hierarchy as needed.
        ''' It reuses existing namespace nodes when found.
        ''' </remarks>
        Private Function GetOrCreateNamespace(vNamespaceName As String) As SyntaxNode
            Try
                If String.IsNullOrEmpty(vNamespaceName) Then
                    Return pRootNamespace
                End If
                
                ' Split namespace into parts for nested namespaces
                Dim lParts() As String = vNamespaceName.Split("."c)
                Dim lCurrentParent As SyntaxNode = pRootNode ' Start from document root
                Dim lCurrentNamespace As SyntaxNode = Nothing
                Dim lAccumulatedName As String = ""
                
                ' Build up the namespace hierarchy
                for each lPart As String in lParts
                    If String.IsNullOrWhiteSpace(lPart) Then Continue for
                    
                    ' Build accumulated name (e.g., "SimpleIDE" then "SimpleIDE.Models")
                    If String.IsNullOrEmpty(lAccumulatedName) Then
                        lAccumulatedName = lPart
                    Else
                        lAccumulatedName &= "." & lPart
                    End If
                    
                    ' Look for existing namespace node with this name
                    lCurrentNamespace = Nothing
                    for each lChild in lCurrentParent.Children
                        If lChild.NodeType = CodeNodeType.eNamespace AndAlso 
                           String.Equals(lChild.Name, lAccumulatedName, StringComparison.OrdinalIgnoreCase) Then
                            lCurrentNamespace = lChild
                            Exit for
                        End If
                    Next
                    
                    ' If not found, create it
                    If lCurrentNamespace Is Nothing Then
                        lCurrentNamespace = New SyntaxNode(CodeNodeType.eNamespace, lAccumulatedName)
                        lCurrentNamespace.FilePath = pCurrentFile
                        lCurrentNamespace.StartLine = pCurrentLineNumber
                        lCurrentNamespace.EndLine = Integer.MaxValue ' Will be updated later
                        lCurrentNamespace.IsImplicit = False ' Explicitly declared namespace
                        
                        ' Add to parent
                        lCurrentParent.AddChild(lCurrentNamespace)
                        Console.WriteLine($"  Created namespace: {lAccumulatedName}")
                    End If
                    
                    ' Move to next level
                    lCurrentParent = lCurrentNamespace
                Next
                
                Return If(lCurrentNamespace, pRootNamespace)
                
            Catch ex As Exception
                Console.WriteLine($"GetOrCreateNamespace error: {ex.Message}")
                Return pRootNamespace
            End Try
        End Function
        
        ''' <summary>
        ''' Extracts the namespace name from a Namespace declaration line
        ''' </summary>
        ''' <param name="vLine">The line containing "Namespace XXX"</param>
        ''' <returns>The namespace name or empty string if not found</returns>
        Private Function ExtractNamespaceName(vLine As String) As String
            Try
                ' Remove "Namespace" keyword and trim
                Dim lPattern As String = "^\s*Namespace\s+(.+?)(\s|$)"
                Dim lMatch As Match = Regex.Match(vLine, lPattern, RegexOptions.IgnoreCase)
                
                If lMatch.Success Then
                    Return lMatch.Groups(1).Value.Trim()
                End If
                
                ' Fallback: simple string manipulation
                Dim lIndex As Integer = vLine.IndexOf("Namespace", StringComparison.OrdinalIgnoreCase)
                If lIndex >= 0 Then
                    Dim lName As String = vLine.Substring(lIndex + 9).Trim()
                    ' Remove any trailing comments
                    Dim lCommentIndex As Integer = lName.IndexOf("'"c)
                    If lCommentIndex >= 0 Then
                        lName = lName.Substring(0, lCommentIndex).Trim()
                    End If
                    Return lName
                End If
                
                Return ""
                
            Catch ex As Exception
                Console.WriteLine($"ExtractNamespaceName error: {ex.Message}")
                Return ""
            End Try
        End Function

        ''' <summary>
        ''' Checks if a line is a method or function declaration
        ''' </summary>
        ''' <param name="vLine">The line to check</param>
        ''' <returns>True if this is a Sub or Function declaration</returns>
        ''' <remarks>
        ''' This checks for method signatures but excludes Declare statements and event handlers
        ''' </remarks>
        Private Function IsMethodDeclaration(vLine As String) As Boolean
            Try
                ' Must contain Sub or Function keyword
                If Not Regex.IsMatch(vLine, "\b(Sub|Function)\b", RegexOptions.IgnoreCase) Then
                    Return False
                End If
                
                ' Exclude Declare statements (API declarations)
                If Regex.IsMatch(vLine, "^\s*Declare\s", RegexOptions.IgnoreCase) Then
                    Return False
                End If
                
                ' Exclude End Sub/End Function
                If Regex.IsMatch(vLine, "^\s*End\s+(Sub|Function)\b", RegexOptions.IgnoreCase) Then
                    Return False
                End If
                
                ' Exclude Exit Sub/Exit Function
                If Regex.IsMatch(vLine, "^\s*Exit\s+(Sub|Function)\b", RegexOptions.IgnoreCase) Then
                    Return False
                End If
                
                ' Check for method pattern: [modifiers] Sub/Function Name[(parameters)]
                Dim lPattern As String = "^\s*(Public|Private|Protected|Friend|Shared|Overrides|Overridable|MustOverride|NotOverridable|Shadows|Partial|Async)*\s*(Sub|Function)\s+\w+"
                Return Regex.IsMatch(vLine, lPattern, RegexOptions.IgnoreCase)
                
            Catch ex As Exception
                Console.WriteLine($"IsMethodDeclaration error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Tokenizes a line of VB.NET code into individual tokens
        ''' </summary>
        ''' <param name="vLine">The line to tokenize</param>
        ''' <returns>List of tokens</returns>
        ''' <remarks>
        ''' This handles VB.NET syntax including strings, comments, and operators
        ''' </remarks>
        Private Function TokenizeLine(vLine As String) As List(Of String)
            Try
                Dim lTokens As New List(Of String)()
                If String.IsNullOrEmpty(vLine) Then Return lTokens
                
                Dim lCurrentToken As New System.Text.StringBuilder()
                Dim lInString As Boolean = False
                Dim lInComment As Boolean = False
                Dim i As Integer = 0
                
                While i < vLine.Length
                    Dim lChar As Char = vLine(i)
                    
                    ' Handle comments
                    If Not lInString AndAlso lChar = "'"c Then
                        ' Start of comment - ignore rest of line
                        If lCurrentToken.Length > 0 Then
                            lTokens.Add(lCurrentToken.ToString())
                        End If
                        Exit While
                    End If
                    
                    ' Handle strings
                    If lChar = """"c Then
                        If lInString Then
                            ' Check for escaped quote
                            If i + 1 < vLine.Length AndAlso vLine(i + 1) = """"c Then
                                ' Escaped quote
                                lCurrentToken.Append("""""")
                                i += 2
                                Continue While
                            Else
                                ' End of string
                                lCurrentToken.Append(lChar)
                                lTokens.Add(lCurrentToken.ToString())
                                lCurrentToken.Clear()
                                lInString = False
                                i += 1
                                Continue While
                            End If
                        Else
                            ' Start of string
                            If lCurrentToken.Length > 0 Then
                                lTokens.Add(lCurrentToken.ToString())
                                lCurrentToken.Clear()
                            End If
                            lCurrentToken.Append(lChar)
                            lInString = True
                            i += 1
                            Continue While
                        End If
                    End If
                    
                    ' If in string, just append
                    If lInString Then
                        lCurrentToken.Append(lChar)
                        i += 1
                        Continue While
                    End If
                    
                    ' Handle operators and delimiters
                    If "()[]{},.=<>!+-*/:;&|".Contains(lChar) Then
                        ' Save current token if any
                        If lCurrentToken.Length > 0 Then
                            lTokens.Add(lCurrentToken.ToString())
                            lCurrentToken.Clear()
                        End If
                        
                        ' Check for multi-character operators
                        If i + 1 < vLine.Length Then
                            Dim lNextChar As Char = vLine(i + 1)
                            Dim lTwoChar As String = lChar.ToString() & lNextChar.ToString()
                            
                            If lTwoChar = "<>" OrElse lTwoChar = "<=" OrElse lTwoChar = ">=" OrElse
                               lTwoChar = ":=" OrElse lTwoChar = "+=" OrElse lTwoChar = "-=" OrElse
                               lTwoChar = "*=" OrElse lTwoChar = "/=" OrElse lTwoChar = "&=" OrElse
                               lTwoChar = "<<" OrElse lTwoChar = ">>" Then
                                lTokens.Add(lTwoChar)
                                i += 2
                                Continue While
                            End If
                        End If
                        
                        ' Single character operator
                        lTokens.Add(lChar.ToString())
                        i += 1
                        Continue While
                    End If
                    
                    ' Handle whitespace
                    If Char.IsWhiteSpace(lChar) Then
                        If lCurrentToken.Length > 0 Then
                            lTokens.Add(lCurrentToken.ToString())
                            lCurrentToken.Clear()
                        End If
                        i += 1
                        Continue While
                    End If
                    
                    ' Regular character
                    lCurrentToken.Append(lChar)
                    i += 1
                End While
                
                ' Add final token if any
                If lCurrentToken.Length > 0 Then
                    lTokens.Add(lCurrentToken.ToString())
                End If
                
                Return lTokens
                
            Catch ex As Exception
                Console.WriteLine($"TokenizeLine error: {ex.Message}")
                Return New List(Of String)()
            End Try
        End Function
    
        
    End Class

End Namespace
