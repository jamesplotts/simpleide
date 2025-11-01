' RoslynConverter.vb - Converts between Roslyn and SimpleIDE representations
' Part of the Roslyn parser replacement
' Created: 2025-01-01

Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax

' Add these aliases:
Imports RoslynSyntaxNode = Microsoft.CodeAnalysis.SyntaxNode
Imports SimpleSyntaxNode = SimpleIDE.Syntax.SyntaxNode

Namespace Managers
    
    ''' <summary>
    ''' Converts between Roslyn and SimpleIDE representations
    ''' </summary>
    Public Class RoslynConverter
        
        ' ===== Private Fields =====
        Private pCurrentFilePath As String
        
        ' ===== Public Methods =====
        
        ''' <summary>
        ''' Converts a Roslyn SyntaxTree to SimpleIDE SyntaxNode tree
        ''' </summary>
        Public Function ConvertToSimpleIDE(vRoslynTree As Microsoft.CodeAnalysis.SyntaxTree, vFilePath As String) As SimpleSyntaxNode
            Try
                pCurrentFilePath = vFilePath
                Dim lRoot = vRoslynTree.GetRoot()
                
                ' Create root node for the file
                Dim lFileRoot As New SyntaxNode(CodeNodeType.eFile, IO.Path.GetFileName(vFilePath))
                lFileRoot.FilePath = vFilePath
                
                ' Process compilation unit
                If TypeOf lRoot Is CompilationUnitSyntax Then
                    Dim lCompilationUnit = DirectCast(lRoot, CompilationUnitSyntax)
                    ProcessCompilationUnit(lCompilationUnit, lFileRoot)
                End If
                
                Return lFileRoot
                
            Catch ex As Exception
                Console.WriteLine($"ConvertToSimpleIDE error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Converts Roslyn tokens to CharacterToken array for rendering
        ''' </summary>
        Public Function ConvertToCharacterTokens(vTokens As IEnumerable(Of Microsoft.CodeAnalysis.SyntaxToken), vLineLength As Integer) As Byte()
            Try
                If vLineLength <= 0 Then Return New Byte() {}
                
                Dim lResult(vLineLength - 1) As Byte
                Dim lDefaultToken = CharacterToken.CreateDefault()
                
                ' Initialize with default tokens
                for i = 0 To vLineLength - 1
                    lResult(i) = lDefaultToken
                Next
                
                ' Apply token colors
                for each lToken in vTokens
                    If lToken.Span.Length > 0 Then
                        Dim lTokenType = ConvertTokenType(lToken)
                        Dim lIsBold = IsKeyword(lToken)
                        Dim lIsItalic = False
                        
                        ' Check for comments in trivia
                        If lToken.HasLeadingTrivia Then
                            for each lTrivia in lToken.LeadingTrivia
                                If lTrivia.IsKind(SyntaxKind.CommentTrivia) OrElse
                                   lTrivia.IsKind(SyntaxKind.DocumentationCommentExteriorTrivia) Then
                                    lIsItalic = True
                                    lTokenType = CharacterTokenType.eComment
                                End If
                            Next
                        End If
                        
                        Dim lEncodedToken = CharacterToken.Encode(lTokenType, lIsBold, lIsItalic)
                        
                        ' Apply to character range
                        Dim lStartPos = lToken.SpanStart
                        Dim lEndPos = Math.Min(lToken.Span.End, vLineLength)
                        
                        for i = lStartPos To lEndPos - 1
                            If i >= 0 AndAlso i < vLineLength Then
                                lResult(i) = lEncodedToken
                            End If
                        Next
                    End If
                Next
                
                Return lResult
                
            Catch ex As Exception
                Console.WriteLine($"ConvertToCharacterTokens error: {ex.Message}")
                Return New Byte(vLineLength - 1) {}
            End Try
        End Function
        
        ''' <summary>
        ''' Converts Roslyn token type to SimpleIDE CharacterTokenType
        ''' </summary>
        Public Function ConvertTokenType(vToken As Microsoft.CodeAnalysis.SyntaxToken) As CharacterTokenType
            Try
                Select Case vToken.Kind()
                    ' Keywords
                    Case SyntaxKind.ClassKeyword, SyntaxKind.ModuleKeyword, SyntaxKind.InterfaceKeyword,
                         SyntaxKind.StructureKeyword, SyntaxKind.EnumKeyword, SyntaxKind.SubKeyword,
                         SyntaxKind.FunctionKeyword, SyntaxKind.PropertyKeyword, SyntaxKind.EventKeyword,
                         SyntaxKind.IfKeyword, SyntaxKind.ThenKeyword, SyntaxKind.ElseKeyword,
                         SyntaxKind.ForKeyword, SyntaxKind.NextKeyword, SyntaxKind.WhileKeyword,
                         SyntaxKind.DoKeyword, SyntaxKind.LoopKeyword, SyntaxKind.SelectKeyword,
                         SyntaxKind.CaseKeyword, SyntaxKind.TryKeyword, SyntaxKind.CatchKeyword,
                         SyntaxKind.FinallyKeyword, SyntaxKind.ThrowKeyword, SyntaxKind.ReturnKeyword,
                         SyntaxKind.ImportsKeyword, SyntaxKind.NamespaceKeyword, SyntaxKind.EndKeyword,
                         SyntaxKind.PublicKeyword, SyntaxKind.PrivateKeyword, SyntaxKind.ProtectedKeyword,
                         SyntaxKind.FriendKeyword, SyntaxKind.SharedKeyword, SyntaxKind.OverridesKeyword,
                         SyntaxKind.OverridableKeyword, SyntaxKind.MustOverrideKeyword,
                         SyntaxKind.NotOverridableKeyword, SyntaxKind.InheritsKeyword,
                         SyntaxKind.ImplementsKeyword, SyntaxKind.AsKeyword, SyntaxKind.NewKeyword,
                         SyntaxKind.DimKeyword, SyntaxKind.ConstKeyword, SyntaxKind.WithEventsKeyword,
                         SyntaxKind.ByValKeyword, SyntaxKind.ByRefKeyword, SyntaxKind.OptionalKeyword,
                         SyntaxKind.ParamArrayKeyword, SyntaxKind.WithKeyword, SyntaxKind.UsingKeyword,
                         SyntaxKind.GetKeyword, SyntaxKind.SetKeyword, SyntaxKind.PartialKeyword,
                         SyntaxKind.MustInheritKeyword, SyntaxKind.NotInheritableKeyword,
                         SyntaxKind.ReadOnlyKeyword, SyntaxKind.WriteOnlyKeyword, SyntaxKind.DefaultKeyword,
                         SyntaxKind.ShadowsKeyword, SyntaxKind.AsyncKeyword, SyntaxKind.AwaitKeyword,
                         SyntaxKind.IteratorKeyword, SyntaxKind.YieldKeyword
                        Return CharacterTokenType.eKeyword
                        
                    ' String literals
                    Case SyntaxKind.StringLiteralToken
                        Return CharacterTokenType.eString
                        
                    ' Numeric literals
                    Case SyntaxKind.IntegerLiteralToken, SyntaxKind.DecimalLiteralToken,
                         SyntaxKind.FloatingLiteralToken
                        Return CharacterTokenType.eNumber
                        
                    ' Identifiers (could be types or variables)
                    Case SyntaxKind.IdentifierToken
                        Return CharacterTokenType.eIdentifier
                        
                    ' Operators
                    Case SyntaxKind.PlusToken, SyntaxKind.MinusToken, SyntaxKind.AsteriskToken,
                         SyntaxKind.SlashToken, SyntaxKind.EqualsToken, SyntaxKind.LessThanToken,
                         SyntaxKind.GreaterThanToken, SyntaxKind.AmpersandToken, SyntaxKind.CaretToken,
                         SyntaxKind.BackslashToken, SyntaxKind.ModKeyword, SyntaxKind.AndKeyword,
                         SyntaxKind.OrKeyword, SyntaxKind.XorKeyword, SyntaxKind.NotKeyword
                        Return CharacterTokenType.eOperator
                        
                    ' Comments (usually handled in trivia, but just in case)
                    Case SyntaxKind.CommentTrivia, SyntaxKind.DocumentationCommentExteriorTrivia,
                         SyntaxKind.DocumentationCommentTrivia
                        Return CharacterTokenType.eComment
                        
                    Case Else
                        Return CharacterTokenType.eText
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"ConvertTokenType error: {ex.Message}")
                Return CharacterTokenType.eText
            End Try
        End Function
        
        ' ===== Private Methods - Roslyn to SimpleIDE Conversion =====
        
        ''' <summary>
        ''' Processes a compilation unit
        ''' </summary>
        Private Sub ProcessCompilationUnit(vUnit As CompilationUnitSyntax, vParent As SimpleSyntaxNode)
            Try
                ' Process imports (skip - not typically shown in Object Explorer)
                ' For Each lImport In vUnit.Imports
                '     ProcessImports(lImport, vParent)
                ' Next
                
                ' Process members (namespaces, types, etc.)
                for each lMember in vUnit.Members
                    ProcessMember(lMember, vParent)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ProcessCompilationUnit error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes a member declaration
        ''' </summary>
        Private Sub ProcessMember(vMember As StatementSyntax, vParent As SimpleSyntaxNode)
            Try
                Select Case vMember.Kind()
                    Case SyntaxKind.NamespaceBlock
                        ProcessNamespace(DirectCast(vMember, NamespaceBlockSyntax), vParent)
                        
                    Case SyntaxKind.ClassBlock
                        ProcessClass(DirectCast(vMember, ClassBlockSyntax), vParent)
                        
                    Case SyntaxKind.ModuleBlock
                        ProcessModule(DirectCast(vMember, ModuleBlockSyntax), vParent)
                        
                    Case SyntaxKind.InterfaceBlock
                        ProcessInterface(DirectCast(vMember, InterfaceBlockSyntax), vParent)
                        
                    Case SyntaxKind.StructureBlock
                        ProcessStructure(DirectCast(vMember, StructureBlockSyntax), vParent)
                        
                    Case SyntaxKind.EnumBlock
                        ProcessEnum(DirectCast(vMember, EnumBlockSyntax), vParent)
                        
                    Case SyntaxKind.DelegateStatement
                        ProcessDelegate(DirectCast(vMember, DelegateStatementSyntax), vParent)
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"ProcessMember error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes a namespace declaration
        ''' </summary>
        Private Sub ProcessNamespace(vNamespace As NamespaceBlockSyntax, vParent As SimpleSyntaxNode)
            Try
                Dim lNamespaceNode As New SyntaxNode(
                    CodeNodeType.eNamespace,
                    vNamespace.NamespaceStatement.Name.ToString()
                )
                
                lNamespaceNode.FilePath = pCurrentFilePath
                lNamespaceNode.StartLine = GetLineNumber(vNamespace)
                lNamespaceNode.EndLine = GetLineNumber(vNamespace.EndNamespaceStatement)
                
                vParent.AddChild(lNamespaceNode)
                
                ' Process namespace members
                for each lMember in vNamespace.Members
                    ProcessMember(lMember, lNamespaceNode)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ProcessNamespace error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes a class declaration
        ''' </summary>
        Private Sub ProcessClass(vClass As ClassBlockSyntax, vParent As SimpleSyntaxNode)
            Try
                Dim lClassNode As New SyntaxNode(
                    CodeNodeType.eClass,
                    vClass.ClassStatement.Identifier.Text
                )
                
                lClassNode.FilePath = pCurrentFilePath
                lClassNode.StartLine = GetLineNumber(vClass)
                lClassNode.EndLine = GetLineNumber(vClass.EndClassStatement)
                
                ' Extract modifiers
                ExtractModifiers(vClass.ClassStatement.Modifiers, lClassNode)
                
                ' Extract inheritance
                If vClass.Inherits IsNot Nothing Then
                    for each lInherits in vClass.Inherits
                        for each lType in lInherits.Types
                            lClassNode.BaseType = lType.ToString()
                            lClassNode.InheritsList.Add(lType.ToString())
                        Next
                    Next
                End If
                
                ' Extract implements
                If vClass.Implements IsNot Nothing Then
                    for each lImplements in vClass.Implements
                        for each lType in lImplements.Types
                            lClassNode.ImplementsList.Add(lType.ToString())
                        Next
                    Next
                End If
                
                ' Extract XML documentation
                ExtractXmlDocumentation(vClass, lClassNode)
                
                vParent.AddChild(lClassNode)
                
                ' Process class members
                for each lMember in vClass.Members
                    ProcessClassMember(lMember, lClassNode)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ProcessClass error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes a module declaration
        ''' </summary>
        Private Sub ProcessModule(vModule As ModuleBlockSyntax, vParent As SimpleSyntaxNode)
            Try
                Dim lModuleNode As New SyntaxNode(
                    CodeNodeType.eModule,
                    vModule.ModuleStatement.Identifier.Text
                )
                
                lModuleNode.FilePath = pCurrentFilePath
                lModuleNode.StartLine = GetLineNumber(vModule)
                lModuleNode.EndLine = GetLineNumber(vModule.EndModuleStatement)
                
                ExtractModifiers(vModule.ModuleStatement.Modifiers, lModuleNode)
                ExtractXmlDocumentation(vModule, lModuleNode)
                
                vParent.AddChild(lModuleNode)
                
                ' Process module members
                for each lMember in vModule.Members
                    ProcessClassMember(lMember, lModuleNode)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ProcessModule error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes an interface declaration
        ''' </summary>
        Private Sub ProcessInterface(vInterface As InterfaceBlockSyntax, vParent As SimpleSyntaxNode)
            Try
                Dim lInterfaceNode As New SyntaxNode(
                    CodeNodeType.eInterface,
                    vInterface.InterfaceStatement.Identifier.Text
                )
                
                lInterfaceNode.FilePath = pCurrentFilePath
                lInterfaceNode.StartLine = GetLineNumber(vInterface)
                lInterfaceNode.EndLine = GetLineNumber(vInterface.EndInterfaceStatement)
                
                ExtractModifiers(vInterface.InterfaceStatement.Modifiers, lInterfaceNode)
                
                ' Extract inherits (interfaces can inherit from other interfaces)
                If vInterface.Inherits IsNot Nothing Then
                    for each lInherits in vInterface.Inherits
                        for each lType in lInherits.Types
                            lInterfaceNode.InheritsList.Add(lType.ToString())
                        Next
                    Next
                End If
                
                ExtractXmlDocumentation(vInterface, lInterfaceNode)
                
                vParent.AddChild(lInterfaceNode)
                
                ' Process interface members
                for each lMember in vInterface.Members
                    ProcessClassMember(lMember, lInterfaceNode)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ProcessInterface error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes a structure declaration
        ''' </summary>
        Private Sub ProcessStructure(vStructure As StructureBlockSyntax, vParent As SimpleSyntaxNode)
            Try
                Dim lStructureNode As New SyntaxNode(
                    CodeNodeType.eStructure,
                    vStructure.StructureStatement.Identifier.Text
                )
                
                lStructureNode.FilePath = pCurrentFilePath
                lStructureNode.StartLine = GetLineNumber(vStructure)
                lStructureNode.EndLine = GetLineNumber(vStructure.EndStructureStatement)
                
                ExtractModifiers(vStructure.StructureStatement.Modifiers, lStructureNode)
                
                ' Extract implements
                If vStructure.Implements IsNot Nothing Then
                    for each lImplements in vStructure.Implements
                        for each lType in lImplements.Types
                            lStructureNode.ImplementsList.Add(lType.ToString())
                        Next
                    Next
                End If
                
                ExtractXmlDocumentation(vStructure, lStructureNode)
                
                vParent.AddChild(lStructureNode)
                
                ' Process structure members
                for each lMember in vStructure.Members
                    ProcessClassMember(lMember, lStructureNode)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ProcessStructure error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes an enum declaration
        ''' </summary>
        Private Sub ProcessEnum(vEnum As EnumBlockSyntax, vParent As SimpleSyntaxNode)
            Try
                Dim lEnumNode As New SyntaxNode(
                    CodeNodeType.eEnum,
                    vEnum.EnumStatement.Identifier.Text
                )
                
                lEnumNode.FilePath = pCurrentFilePath
                lEnumNode.StartLine = GetLineNumber(vEnum)
                lEnumNode.EndLine = GetLineNumber(vEnum.EndEnumStatement)
                
                ExtractModifiers(vEnum.EnumStatement.Modifiers, lEnumNode)
                ExtractXmlDocumentation(vEnum, lEnumNode)
                
                vParent.AddChild(lEnumNode)
                
                ' Process enum members
                for each lMember in vEnum.Members
                    Dim lEnumValueNode As New SyntaxNode(
                        CodeNodeType.eEnumValue,
                        lMember.Identifier.Text
                    )
                    
                    lEnumValueNode.FilePath = pCurrentFilePath
                    lEnumValueNode.StartLine = GetLineNumber(lMember)
                    
                    ExtractXmlDocumentation(lMember, lEnumValueNode)
                    
                    lEnumNode.AddChild(lEnumValueNode)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ProcessEnum error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes a delegate declaration
        ''' </summary>
        Private Sub ProcessDelegate(vDelegate As DelegateStatementSyntax, vParent As SimpleSyntaxNode)
            Try
                Dim lDelegateNode As New SyntaxNode(
                    CodeNodeType.eDelegate,
                    vDelegate.Identifier.Text
                )
                
                lDelegateNode.FilePath = pCurrentFilePath
                lDelegateNode.StartLine = GetLineNumber(vDelegate)
                
                ExtractModifiers(vDelegate.Modifiers, lDelegateNode)
                ExtractParameters(vDelegate.ParameterList, lDelegateNode)
                
                ' Extract return type for function delegates
                If vDelegate.AsClause IsNot Nothing Then
                    lDelegateNode.ReturnType = vDelegate.AsClause.Type.ToString()
                End If
                
                ExtractXmlDocumentation(vDelegate, lDelegateNode)
                
                vParent.AddChild(lDelegateNode)
                
            Catch ex As Exception
                Console.WriteLine($"ProcessDelegate error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes a class/module/interface member
        ''' </summary>
        Private Sub ProcessClassMember(vMember As StatementSyntax, vParent As SimpleSyntaxNode)
            Try
                Select Case vMember.Kind()
                    Case SyntaxKind.SubBlock, SyntaxKind.FunctionBlock
                        ProcessMethod(DirectCast(vMember, MethodBlockSyntax), vParent)
                        
                    Case SyntaxKind.ConstructorBlock
                        ProcessConstructor(DirectCast(vMember, ConstructorBlockSyntax), vParent)
                        
                    Case SyntaxKind.PropertyBlock
                        ProcessProperty(DirectCast(vMember, PropertyBlockSyntax), vParent)
                        
                    Case SyntaxKind.PropertyStatement
                        ProcessAutoProperty(DirectCast(vMember, PropertyStatementSyntax), vParent)
                        
                    Case SyntaxKind.EventBlock
                        ProcessEventBlock(DirectCast(vMember, EventBlockSyntax), vParent)
                        
                    Case SyntaxKind.EventStatement
                        ProcessEventStatement(DirectCast(vMember, EventStatementSyntax), vParent)
                        
                    Case SyntaxKind.FieldDeclaration
                        ProcessField(DirectCast(vMember, FieldDeclarationSyntax), vParent)
                        
                    Case SyntaxKind.DelegateStatement
                        ProcessDelegate(DirectCast(vMember, DelegateStatementSyntax), vParent)
                        
                    ' Handle nested types
                    Case SyntaxKind.ClassBlock
                        ProcessClass(DirectCast(vMember, ClassBlockSyntax), vParent)
                        
                    Case SyntaxKind.StructureBlock
                        ProcessStructure(DirectCast(vMember, StructureBlockSyntax), vParent)
                        
                    Case SyntaxKind.EnumBlock
                        ProcessEnum(DirectCast(vMember, EnumBlockSyntax), vParent)
                        
                    Case SyntaxKind.InterfaceBlock
                        ProcessInterface(DirectCast(vMember, InterfaceBlockSyntax), vParent)
                        
                    Case SyntaxKind.ModuleBlock
                        ProcessModule(DirectCast(vMember, ModuleBlockSyntax), vParent)
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"ProcessClassMember error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes a method (Sub or Function)
        ''' </summary>
        Private Sub ProcessMethod(vMethod As MethodBlockSyntax, vParent As SimpleSyntaxNode)
            Try
                Dim lIsFunction = vMethod.SubOrFunctionStatement.DeclarationKeyword.IsKind(SyntaxKind.FunctionKeyword)
                
                Dim lMethodNode As New SyntaxNode(
                    If(lIsFunction, CodeNodeType.eFunction, CodeNodeType.eMethod),
                    vMethod.SubOrFunctionStatement.Identifier.Text
                )
                
                lMethodNode.FilePath = pCurrentFilePath
                lMethodNode.StartLine = GetLineNumber(vMethod)
                lMethodNode.EndLine = GetLineNumber(vMethod.EndSubOrFunctionStatement)
                
                ExtractModifiers(vMethod.SubOrFunctionStatement.Modifiers, lMethodNode)
                ExtractParameters(vMethod.SubOrFunctionStatement.ParameterList, lMethodNode)
                
                ' Extract return type for functions
                If lIsFunction AndAlso vMethod.SubOrFunctionStatement.AsClause IsNot Nothing Then
                    lMethodNode.ReturnType = vMethod.SubOrFunctionStatement.AsClause.Type.ToString()
                End If
                
                ExtractXmlDocumentation(vMethod, lMethodNode)
                
                vParent.AddChild(lMethodNode)
                
            Catch ex As Exception
                Console.WriteLine($"ProcessMethod error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes a constructor
        ''' </summary>
        Private Sub ProcessConstructor(vConstructor As ConstructorBlockSyntax, vParent As SimpleSyntaxNode)
            Try
                Dim lConstructorNode As New SyntaxNode(CodeNodeType.eConstructor, "New")
                
                lConstructorNode.FilePath = pCurrentFilePath
                lConstructorNode.StartLine = GetLineNumber(vConstructor)
                lConstructorNode.EndLine = GetLineNumber(vConstructor.EndSubStatement)
                
                ExtractModifiers(vConstructor.SubNewStatement.Modifiers, lConstructorNode)
                ExtractParameters(vConstructor.SubNewStatement.ParameterList, lConstructorNode)
                ExtractXmlDocumentation(vConstructor, lConstructorNode)
                
                vParent.AddChild(lConstructorNode)
                
            Catch ex As Exception
                Console.WriteLine($"ProcessConstructor error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes a property block
        ''' </summary>
        Private Sub ProcessProperty(vProperty As PropertyBlockSyntax, vParent As SimpleSyntaxNode)
            Try
                Dim lPropertyNode As New SyntaxNode(
                    CodeNodeType.eProperty,
                    vProperty.PropertyStatement.Identifier.Text
                )
                
                lPropertyNode.FilePath = pCurrentFilePath
                lPropertyNode.StartLine = GetLineNumber(vProperty)
                lPropertyNode.EndLine = GetLineNumber(vProperty.EndPropertyStatement)
                
                ExtractModifiers(vProperty.PropertyStatement.Modifiers, lPropertyNode)
                
                ' Extract property type
                If vProperty.PropertyStatement.AsClause IsNot Nothing Then
                    lPropertyNode.ReturnType = vProperty.PropertyStatement.AsClause.Type.ToString()
                End If
                
                ' Extract parameters (for indexed properties)
                ExtractParameters(vProperty.PropertyStatement.ParameterList, lPropertyNode)
                
                ExtractXmlDocumentation(vProperty, lPropertyNode)
                
                vParent.AddChild(lPropertyNode)
                
            Catch ex As Exception
                Console.WriteLine($"ProcessProperty error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes an auto-implemented property
        ''' </summary>
        Private Sub ProcessAutoProperty(vProperty As PropertyStatementSyntax, vParent As SimpleSyntaxNode)
            Try
                Dim lPropertyNode As New SyntaxNode(
                    CodeNodeType.eProperty,
                    vProperty.Identifier.Text
                )
                
                lPropertyNode.FilePath = pCurrentFilePath
                lPropertyNode.StartLine = GetLineNumber(vProperty)
                
                ExtractModifiers(vProperty.Modifiers, lPropertyNode)
                
                ' Extract property type
                If vProperty.AsClause IsNot Nothing Then
                    lPropertyNode.ReturnType = vProperty.AsClause.Type.ToString()
                End If
                
                ' Extract parameters (for indexed properties)
                ExtractParameters(vProperty.ParameterList, lPropertyNode)
                
                ExtractXmlDocumentation(vProperty, lPropertyNode)
                
                vParent.AddChild(lPropertyNode)
                
            Catch ex As Exception
                Console.WriteLine($"ProcessAutoProperty error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes an event block
        ''' </summary>
        Private Sub ProcessEventBlock(vEvent As EventBlockSyntax, vParent As SimpleSyntaxNode)
            Try
                Dim lEventNode As New SyntaxNode(
                    CodeNodeType.eEvent,
                    vEvent.EventStatement.Identifier.Text
                )
                
                lEventNode.FilePath = pCurrentFilePath
                lEventNode.StartLine = GetLineNumber(vEvent)
                lEventNode.EndLine = GetLineNumber(vEvent.EndEventStatement)
                
                ExtractModifiers(vEvent.EventStatement.Modifiers, lEventNode)
                ExtractParameters(vEvent.EventStatement.ParameterList, lEventNode)
                ExtractXmlDocumentation(vEvent, lEventNode)
                
                vParent.AddChild(lEventNode)
                
            Catch ex As Exception
                Console.WriteLine($"ProcessEventBlock error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes an event statement
        ''' </summary>
        Private Sub ProcessEventStatement(vEvent As EventStatementSyntax, vParent As SimpleSyntaxNode)
            Try
                Dim lEventNode As New SyntaxNode(
                    CodeNodeType.eEvent,
                    vEvent.Identifier.Text
                )
                
                lEventNode.FilePath = pCurrentFilePath
                lEventNode.StartLine = GetLineNumber(vEvent)
                
                ExtractModifiers(vEvent.Modifiers, lEventNode)
                ExtractParameters(vEvent.ParameterList, lEventNode)
                ExtractXmlDocumentation(vEvent, lEventNode)
                
                vParent.AddChild(lEventNode)
                
            Catch ex As Exception
                Console.WriteLine($"ProcessEventStatement error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes a field declaration
        ''' </summary>
        Private Sub ProcessField(vField As FieldDeclarationSyntax, vParent As SimpleSyntaxNode)
            Try
                Dim lModifiers = vField.Modifiers
                
                for each lDeclarator in vField.Declarators
                    for each lName in lDeclarator.Names
                        Dim lFieldNode As New SyntaxNode(
                            CodeNodeType.eField,
                            lName.Identifier.Text
                        )
                        
                        lFieldNode.FilePath = pCurrentFilePath
                        lFieldNode.StartLine = GetLineNumber(vField)
                        
                        ExtractModifiers(lModifiers, lFieldNode)
                        
                        ' Extract field type
                        If lDeclarator.AsClause IsNot Nothing Then
                            lFieldNode.ReturnType = lDeclarator.AsClause.Type.ToString()
                        End If
                        
                        ExtractXmlDocumentation(vField, lFieldNode)
                        
                        vParent.AddChild(lFieldNode)
                    Next
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ProcessField error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Helper Methods =====
        
        ''' <summary>
        ''' Extracts modifiers from a token list
        ''' </summary>
        Private Sub ExtractModifiers(vModifiers As SyntaxTokenList, vNode As SimpleSyntaxNode)
            Try
                for each lModifier in vModifiers
                    Select Case lModifier.Kind()
                        Case SyntaxKind.PublicKeyword
                            vNode.IsPublic = True
                            vNode.Visibility = SyntaxNode.eVisibility.ePublic
                        Case SyntaxKind.PrivateKeyword
                            vNode.IsPrivate = True
                            vNode.Visibility = SyntaxNode.eVisibility.ePrivate
                        Case SyntaxKind.ProtectedKeyword
                            vNode.IsProtected = True
                            If vNode.IsFriend Then
                                vNode.Visibility = SyntaxNode.eVisibility.eProtectedFriend
                            Else
                                vNode.Visibility = SyntaxNode.eVisibility.eProtected
                            End If
                        Case SyntaxKind.FriendKeyword
                            vNode.IsFriend = True
                            If vNode.IsProtected Then
                                vNode.Visibility = SyntaxNode.eVisibility.eProtectedFriend
                            Else
                                vNode.Visibility = SyntaxNode.eVisibility.eFriend
                            End If
                        Case SyntaxKind.SharedKeyword
                            vNode.IsShared = True
                        Case SyntaxKind.OverridesKeyword
                            vNode.IsOverrides = True
                        Case SyntaxKind.OverridableKeyword
                            vNode.IsOverridable = True
                        Case SyntaxKind.MustOverrideKeyword
                            vNode.IsMustOverride = True
                        Case SyntaxKind.NotOverridableKeyword
                            vNode.IsNotOverridable = True
                        Case SyntaxKind.MustInheritKeyword
                            vNode.IsMustInherit = True
                        Case SyntaxKind.NotInheritableKeyword
                            vNode.IsNotInheritable = True
                        Case SyntaxKind.PartialKeyword
                            vNode.IsPartial = True
                        Case SyntaxKind.ReadOnlyKeyword
                            vNode.IsReadOnly = True
                        Case SyntaxKind.WriteOnlyKeyword
                            vNode.IsWriteOnly = True
                        Case SyntaxKind.WithEventsKeyword
                            vNode.IsWithEvents = True
                        Case SyntaxKind.ConstKeyword
                            vNode.IsConst = True
                        Case SyntaxKind.ShadowsKeyword
                            vNode.IsShadows = True
                        Case SyntaxKind.AsyncKeyword
                            vNode.IsAsync = True
                        Case SyntaxKind.IteratorKeyword
                            vNode.IsIterator = True
                        Case SyntaxKind.DefaultKeyword
                            vNode.IsDefault = True
                    End Select
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ExtractModifiers error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Extracts parameters from a parameter list
        ''' </summary>
        Private Sub ExtractParameters(vParameterList As ParameterListSyntax, vNode As SimpleSyntaxNode)
            Try
                If vParameterList Is Nothing Then Return
                
                for each lParam in vParameterList.Parameters
                    Dim lParamInfo As New ParameterInfo()
                    lParamInfo.Name = lParam.Identifier.Identifier.Text
                    
                    ' Extract parameter type
                    If lParam.AsClause IsNot Nothing Then
                        lParamInfo.Type = lParam.AsClause.Type.ToString()
                    End If
                    
                    ' Extract modifiers
                    for each lModifier in lParam.Modifiers
                        Select Case lModifier.Kind()
                            Case SyntaxKind.ByRefKeyword
                                lParamInfo.IsByRef = True
                            Case SyntaxKind.ByValKeyword
                                lParamInfo.IsByVal = True
                            Case SyntaxKind.OptionalKeyword
                                lParamInfo.IsOptional = True
                            Case SyntaxKind.ParamArrayKeyword
                                lParamInfo.IsParamArray = True
                        End Select
                    Next
                    
                    ' Extract default value for optional parameters
                    If lParam.Default IsNot Nothing Then
                        lParamInfo.DefaultValue = lParam.Default.Value.ToString()
                    End If
                    
                    vNode.Parameters.Add(lParamInfo)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ExtractParameters error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Extracts XML documentation from a syntax node
        ''' </summary>
        Private Sub ExtractXmlDocumentation(vRoslynNode As SimpleSyntaxNode, vSimpleNode As SimpleSyntaxNode)
            Try
                ' Get leading trivia
                Dim lTrivia = vRoslynNode.GetLeadingTrivia()
                
                Dim lDocInfo As New XmlDocInfo()
                Dim lHasDoc = False
                
                for each lTriviaItem in lTrivia
                    If lTriviaItem.IsKind(SyntaxKind.DocumentationCommentTrivia) Then
                        Dim lStructure = DirectCast(lTriviaItem.GetStructure(), DocumentationCommentTriviaSyntax)
                        
                        for each lContent in lStructure.Content
                            If TypeOf lContent Is XmlElementSyntax Then
                                Dim lElement = DirectCast(lContent, XmlElementSyntax)
                                Dim lTagName = lElement.StartTag.Name.ToString().ToLower()
                                
                                Select Case lTagName
                                    Case "summary"
                                        lDocInfo.Summary = ExtractXmlElementText(lElement)
                                        lHasDoc = True
                                        
                                    Case "remarks"
                                        lDocInfo.Remarks = ExtractXmlElementText(lElement)
                                        
                                    Case "returns"
                                        lDocInfo.Returns = ExtractXmlElementText(lElement)
                                        
                                    Case "value"
                                        lDocInfo.Value = ExtractXmlElementText(lElement)
                                        
                                    Case "example"
                                        lDocInfo.Example = ExtractXmlElementText(lElement)
                                        
                                    Case "param"
                                        Dim lParamName = GetXmlAttribute(lElement.StartTag, "name")
                                        If Not String.IsNullOrEmpty(lParamName) Then
                                            If lDocInfo.Parameters Is Nothing Then
                                                lDocInfo.Parameters = New Dictionary(Of String, String)
                                            End If
                                            lDocInfo.Parameters(lParamName) = ExtractXmlElementText(lElement)
                                        End If
                                        
                                    Case "typeparam"
                                        Dim lTypeParamName = GetXmlAttribute(lElement.StartTag, "name")
                                        If Not String.IsNullOrEmpty(lTypeParamName) Then
                                            If lDocInfo.TypeParameters Is Nothing Then
                                                lDocInfo.TypeParameters = New Dictionary(Of String, String)
                                            End If
                                            lDocInfo.TypeParameters(lTypeParamName) = ExtractXmlElementText(lElement)
                                        End If
                                        
                                    Case "exception"
                                        Dim lExceptionType = GetXmlAttribute(lElement.StartTag, "cref")
                                        If Not String.IsNullOrEmpty(lExceptionType) Then
                                            If lDocInfo.Exceptions Is Nothing Then
                                                lDocInfo.Exceptions = New Dictionary(Of String, String)
                                            End If
                                            lDocInfo.Exceptions(lExceptionType) = ExtractXmlElementText(lElement)
                                        End If
                                End Select
                            End If
                        Next
                    End If
                Next
                
                If lHasDoc Then
                    vSimpleNode.XmlDocumentation = lDocInfo
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ExtractXmlDocumentation error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Extracts text content from an XML element
        ''' </summary>
        Private Function ExtractXmlElementText(vElement As XmlElementSyntax) As String
            Try
                Dim lText As New Text.StringBuilder()
                
                for each lContent in vElement.Content
                    If TypeOf lContent Is XmlTextSyntax Then
                        Dim lXmlText = DirectCast(lContent, XmlTextSyntax)
                        for each lToken in lXmlText.TextTokens
                            lText.Append(lToken.ValueText)
                        Next
                    ElseIf TypeOf lContent Is XmlEmptyElementSyntax Then
                        ' Handle <see cref=""/> and similar tags
                        Dim lEmptyElement = DirectCast(lContent, XmlEmptyElementSyntax)
                        If lEmptyElement.Name.ToString().ToLower() = "see" Then
                            Dim lCref = GetXmlAttribute(lEmptyElement, "cref")
                            If Not String.IsNullOrEmpty(lCref) Then
                                lText.Append(lCref)
                            End If
                        End If
                    ElseIf TypeOf lContent Is XmlElementSyntax Then
                        ' Recursively extract text from nested elements
                        lText.Append(ExtractXmlElementText(DirectCast(lContent, XmlElementSyntax)))
                    End If
                Next
                
                Return lText.ToString().Trim()
                
            Catch ex As Exception
                Console.WriteLine($"ExtractXmlElementText error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ''' <summary>
        ''' Gets an attribute value from an XML start tag
        ''' </summary>
        Private Function GetXmlAttribute(vStartTag As XmlElementStartTagSyntax, vAttributeName As String) As String
            Try
                for each lAttribute in vStartTag.Attributes
                    If TypeOf lAttribute Is XmlNameAttributeSyntax Then
                        Dim lNameAttr = DirectCast(lAttribute, XmlNameAttributeSyntax)
                        If lNameAttr.Name.LocalName.Text.Equals(vAttributeName, StringComparison.OrdinalIgnoreCase) Then
                            Return lNameAttr.Identifier.Identifier.Text
                        End If
                    ElseIf TypeOf lAttribute Is XmlCrefAttributeSyntax Then
                        Dim lCrefAttr = DirectCast(lAttribute, XmlCrefAttributeSyntax)
                        If vAttributeName.Equals("cref", StringComparison.OrdinalIgnoreCase) Then
                            Return lCrefAttr.Cref.ToString()
                        End If
                    End If
                Next
                
                Return ""
                
            Catch ex As Exception
                Console.WriteLine($"GetXmlAttribute error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ''' <summary>
        ''' Gets an attribute value from an XML empty element
        ''' </summary>
        Private Function GetXmlAttribute(vEmptyElement As XmlEmptyElementSyntax, vAttributeName As String) As String
            Try
                for each lAttribute in vEmptyElement.Attributes
                    If TypeOf lAttribute Is XmlNameAttributeSyntax Then
                        Dim lNameAttr = DirectCast(lAttribute, XmlNameAttributeSyntax)
                        If lNameAttr.Name.LocalName.Text.Equals(vAttributeName, StringComparison.OrdinalIgnoreCase) Then
                            Return lNameAttr.Identifier.Identifier.Text
                        End If
                    ElseIf TypeOf lAttribute Is XmlCrefAttributeSyntax Then
                        Dim lCrefAttr = DirectCast(lAttribute, XmlCrefAttributeSyntax)
                        If vAttributeName.Equals("cref", StringComparison.OrdinalIgnoreCase) Then
                            Return lCrefAttr.Cref.ToString()
                        End If
                    End If
                Next
                
                Return ""
                
            Catch ex As Exception
                Console.WriteLine($"GetXmlAttribute (empty element) error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ''' <summary>
        ''' Gets the line number from a syntax node
        ''' </summary>
        Private Function GetLineNumber(vNode As SimpleSyntaxNode) As Integer
            Try
                Dim lLineSpan = vNode.GetLocation().GetLineSpan()
                Return lLineSpan.StartLinePosition.Line
                
            Catch ex As Exception
                Return 0
            End Try
        End Function
        
        ''' <summary>
        ''' Checks if a token is a keyword
        ''' </summary>
        Private Function IsKeyword(vToken As Microsoft.CodeAnalysis.SyntaxToken) As Boolean
            Return vToken.IsKeyword()
        End Function
        
    End Class
    
End Namespace