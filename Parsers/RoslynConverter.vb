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
                Dim lFileRoot As New SimpleSyntaxNode(CodeNodeType.eFile, IO.Path.GetFileName(vFilePath))
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
        
        ' ===== Private Processing Methods =====
        
        ''' <summary>
        ''' Processes the compilation unit (root of file)
        ''' </summary>
        Private Sub ProcessCompilationUnit(vUnit As CompilationUnitSyntax, vParent As SimpleSyntaxNode)
            Try
                ' Process imports
                for each lImport in vUnit.Imports
                    ProcessImport(lImport, vParent)
                Next
                
                ' Process global attributes
                for each lAttrList in vUnit.Attributes
                    ProcessAttributeList(lAttrList, vParent)
                Next
                
                ' Process members (namespaces, classes, modules, etc.)
                for each lMember in vUnit.Members
                    ProcessMember(lMember, vParent)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ProcessCompilationUnit error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes an import statement
        ''' </summary>
        Private Sub ProcessImport(vImport As ImportsStatementSyntax, vParent As SimpleSyntaxNode)
            Try
                for each lClause in vImport.ImportsClauses
                    Dim lImportNode As New SimpleSyntaxNode(
                        CodeNodeType.eImport,
                        lClause.ToString()
                    )
                    
                    lImportNode.FilePath = pCurrentFilePath
                    lImportNode.StartLine = GetLineNumber(DirectCast(vImport, RoslynSyntaxNode))
                    lImportNode.EndLine = lImportNode.StartLine
                    
                    vParent.AddChild(lImportNode)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ProcessImport error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes attribute lists
        ''' </summary>
        Private Sub ProcessAttributeList(vAttrList As AttributesStatementSyntax, vParent As SimpleSyntaxNode)
            Try
                for each lAttrBlock in vAttrList.AttributeLists
                    for each lAttr in lAttrBlock.Attributes
                        ' For attributes, we'll just store them in parent's Attributes dictionary
                        ' instead of creating separate nodes
                        Dim lAttrName As String = lAttr.Name.ToString()
                        Dim lAttrValue As String = ""
                        
                        If lAttr.ArgumentList IsNot Nothing Then
                            lAttrValue = lAttr.ArgumentList.ToString()
                        End If
                        
                        vParent.Attributes($"Attribute_{lAttrName}") = lAttrValue
                    Next
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ProcessAttributeList error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes a member (namespace, class, module, etc.)
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
                        
                    Case SyntaxKind.DelegateFunctionStatement, SyntaxKind.DelegateSubStatement
                        ProcessDelegate(DirectCast(vMember, DelegateStatementSyntax), vParent)
                        
                    Case SyntaxKind.FieldDeclaration
                        ProcessField(DirectCast(vMember, FieldDeclarationSyntax), vParent)
                        
                    Case SyntaxKind.PropertyBlock
                        ProcessPropertyBlock(DirectCast(vMember, PropertyBlockSyntax), vParent)
                        
                    Case SyntaxKind.PropertyStatement
                        ProcessPropertyStatement(DirectCast(vMember, PropertyStatementSyntax), vParent)
                        
                    Case SyntaxKind.SubBlock, SyntaxKind.FunctionBlock
                        ProcessMethodBlock(DirectCast(vMember, MethodBlockSyntax), vParent)
                        
                    Case SyntaxKind.ConstructorBlock
                        ProcessConstructor(DirectCast(vMember, ConstructorBlockSyntax), vParent)
                        
                    Case SyntaxKind.EventBlock
                        ProcessEventBlock(DirectCast(vMember, EventBlockSyntax), vParent)
                        
                    Case SyntaxKind.EventStatement
                        ProcessEventStatement(DirectCast(vMember, EventStatementSyntax), vParent)
                        
                    Case Else
                        ' Handle other member types if needed
                        Console.WriteLine($"Unhandled member kind: {vMember.Kind()}")
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"ProcessMember error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes type members (for classes, modules, etc.)
        ''' </summary>
        Private Sub ProcessTypeMember(vMember As StatementSyntax, vParent As SimpleSyntaxNode)
            Try
                Select Case vMember.Kind()
                    Case SyntaxKind.FieldDeclaration
                        ProcessField(DirectCast(vMember, FieldDeclarationSyntax), vParent)
                        
                    Case SyntaxKind.PropertyBlock
                        ProcessPropertyBlock(DirectCast(vMember, PropertyBlockSyntax), vParent)
                        
                    Case SyntaxKind.PropertyStatement
                        ProcessPropertyStatement(DirectCast(vMember, PropertyStatementSyntax), vParent)
                        
                    Case SyntaxKind.SubBlock, SyntaxKind.FunctionBlock
                        ProcessMethodBlock(DirectCast(vMember, MethodBlockSyntax), vParent)
                        
                    Case SyntaxKind.ConstructorBlock
                        ProcessConstructor(DirectCast(vMember, ConstructorBlockSyntax), vParent)
                        
                    Case SyntaxKind.EventBlock
                        ProcessEventBlock(DirectCast(vMember, EventBlockSyntax), vParent)
                        
                    Case SyntaxKind.EventStatement
                        ProcessEventStatement(DirectCast(vMember, EventStatementSyntax), vParent)
                        
                    Case SyntaxKind.EnumBlock
                        ProcessEnum(DirectCast(vMember, EnumBlockSyntax), vParent)
                        
                    Case SyntaxKind.DelegateFunctionStatement, SyntaxKind.DelegateSubStatement
                        ProcessDelegate(DirectCast(vMember, DelegateStatementSyntax), vParent)
                        
                    Case SyntaxKind.ClassBlock
                        ProcessClass(DirectCast(vMember, ClassBlockSyntax), vParent)
                        
                    Case SyntaxKind.StructureBlock
                        ProcessStructure(DirectCast(vMember, StructureBlockSyntax), vParent)
                        
                    Case SyntaxKind.InterfaceBlock
                        ProcessInterface(DirectCast(vMember, InterfaceBlockSyntax), vParent)
                        
                    Case Else
                        ' Handle other member types if needed
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"ProcessTypeMember error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes a namespace declaration
        ''' </summary>
        Private Sub ProcessNamespace(vNamespace As NamespaceBlockSyntax, vParent As SimpleSyntaxNode)
            Try
                Dim lNamespaceNode As New SimpleSyntaxNode(
                    CodeNodeType.eNamespace,
                    vNamespace.NamespaceStatement.Name.ToString()
                )
                
                lNamespaceNode.FilePath = pCurrentFilePath
                lNamespaceNode.StartLine = GetLineNumber(DirectCast(vNamespace, RoslynSyntaxNode))
                lNamespaceNode.EndLine = GetLineNumber(DirectCast(vNamespace.EndNamespaceStatement, RoslynSyntaxNode))
                
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
                Dim lClassNode As New SimpleSyntaxNode(
                    CodeNodeType.eClass,
                    vClass.ClassStatement.Identifier.Text
                )
                
                lClassNode.FilePath = pCurrentFilePath
                lClassNode.StartLine = GetLineNumber(DirectCast(vClass, RoslynSyntaxNode))
                lClassNode.EndLine = GetLineNumber(DirectCast(vClass.EndClassStatement, RoslynSyntaxNode))
                
                ' Extract modifiers
                ExtractModifiers(vClass.ClassStatement.Modifiers, lClassNode)
                
                ' Extract inheritance
                If vClass.Inherits.Count > 0 Then
                    for each lInherits in vClass.Inherits
                        for each lType in lInherits.Types
                            lClassNode.BaseType = lType.ToString()
                            Exit for ' Take first base type
                        Next
                        Exit for
                    Next
                End If
                
                ' Extract implements
                If vClass.Implements.Count > 0 Then
                    for each lImplements in vClass.Implements
                        for each lType in lImplements.Types
                            lClassNode.ImplementsList.Add(lType.ToString())
                        Next
                    Next
                End If
                
                ' Extract type parameters (generics)
                If vClass.ClassStatement.TypeParameterList IsNot Nothing Then
                    lClassNode.Attributes("TypeParameters") = vClass.ClassStatement.TypeParameterList.ToString()
                End If
                
                ' Extract XML documentation
                ExtractXmlDocumentation(DirectCast(vClass, RoslynSyntaxNode), lClassNode)
                
                vParent.AddChild(lClassNode)
                
                ' Process class members
                for each lMember in vClass.Members
                    ProcessTypeMember(lMember, lClassNode)
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
                Dim lModuleNode As New SimpleSyntaxNode(
                    CodeNodeType.eModule,
                    vModule.ModuleStatement.Identifier.Text
                )
                
                lModuleNode.FilePath = pCurrentFilePath
                lModuleNode.StartLine = GetLineNumber(DirectCast(vModule, RoslynSyntaxNode))
                lModuleNode.EndLine = GetLineNumber(DirectCast(vModule.EndModuleStatement, RoslynSyntaxNode))
                
                ' Extract modifiers
                ExtractModifiers(vModule.ModuleStatement.Modifiers, lModuleNode)
                
                ' Modules are always shared
                lModuleNode.IsShared = True
                
                ' Extract XML documentation
                ExtractXmlDocumentation(DirectCast(vModule, RoslynSyntaxNode), lModuleNode)
                
                vParent.AddChild(lModuleNode)
                
                ' Process module members
                for each lMember in vModule.Members
                    ProcessTypeMember(lMember, lModuleNode)
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
                Dim lInterfaceNode As New SimpleSyntaxNode(
                    CodeNodeType.eInterface,
                    vInterface.InterfaceStatement.Identifier.Text
                )
                
                lInterfaceNode.FilePath = pCurrentFilePath
                lInterfaceNode.StartLine = GetLineNumber(DirectCast(vInterface, RoslynSyntaxNode))
                lInterfaceNode.EndLine = GetLineNumber(DirectCast(vInterface.EndInterfaceStatement, RoslynSyntaxNode))
                
                ' Extract modifiers
                ExtractModifiers(vInterface.InterfaceStatement.Modifiers, lInterfaceNode)
                
                ' Extract inheritance
                If vInterface.Inherits.Count > 0 Then
                    for each lInherits in vInterface.Inherits
                        for each lType in lInherits.Types
                            lInterfaceNode.InheritsList.Add(lType.ToString())
                        Next
                    Next
                End If
                
                ' Extract type parameters (generics)
                If vInterface.InterfaceStatement.TypeParameterList IsNot Nothing Then
                    lInterfaceNode.Attributes("TypeParameters") = vInterface.InterfaceStatement.TypeParameterList.ToString()
                End If
                
                ' Extract XML documentation
                ExtractXmlDocumentation(DirectCast(vInterface, RoslynSyntaxNode), lInterfaceNode)
                
                vParent.AddChild(lInterfaceNode)
                
                ' Process interface members
                for each lMember in vInterface.Members
                    ProcessTypeMember(lMember, lInterfaceNode)
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
                Dim lStructureNode As New SimpleSyntaxNode(
                    CodeNodeType.eStructure,
                    vStructure.StructureStatement.Identifier.Text
                )
                
                lStructureNode.FilePath = pCurrentFilePath
                lStructureNode.StartLine = GetLineNumber(DirectCast(vStructure, RoslynSyntaxNode))
                lStructureNode.EndLine = GetLineNumber(DirectCast(vStructure.EndStructureStatement, RoslynSyntaxNode))
                
                ' Extract modifiers
                ExtractModifiers(vStructure.StructureStatement.Modifiers, lStructureNode)
                
                ' Extract implements
                If vStructure.Implements.Count > 0 Then
                    for each lImplements in vStructure.Implements
                        for each lType in lImplements.Types
                            lStructureNode.ImplementsList.Add(lType.ToString())
                        Next
                    Next
                End If
                
                ' Extract type parameters (generics)
                If vStructure.StructureStatement.TypeParameterList IsNot Nothing Then
                    lStructureNode.Attributes("TypeParameters") = vStructure.StructureStatement.TypeParameterList.ToString()
                End If
                
                ' Extract XML documentation
                ExtractXmlDocumentation(DirectCast(vStructure, RoslynSyntaxNode), lStructureNode)
                
                vParent.AddChild(lStructureNode)
                
                ' Process structure members
                for each lMember in vStructure.Members
                    ProcessTypeMember(lMember, lStructureNode)
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
                Dim lEnumNode As New SimpleSyntaxNode(
                    CodeNodeType.eEnum,
                    vEnum.EnumStatement.Identifier.Text
                )
                
                lEnumNode.FilePath = pCurrentFilePath
                lEnumNode.StartLine = GetLineNumber(DirectCast(vEnum, RoslynSyntaxNode))
                lEnumNode.EndLine = GetLineNumber(DirectCast(vEnum.EndEnumStatement, RoslynSyntaxNode))
                
                ' Extract modifiers
                ExtractModifiers(vEnum.EnumStatement.Modifiers, lEnumNode)
                
                ' Extract XML documentation
                ExtractXmlDocumentation(DirectCast(vEnum, RoslynSyntaxNode), lEnumNode)
                
                vParent.AddChild(lEnumNode)
                
                ' Process enum members
                for each lMember in vEnum.Members
                    Dim lMemberNode As New SimpleSyntaxNode(
                        CodeNodeType.eEnumValue,  ' Use eEnumValue instead of eEnumMember
                        DirectCast(lMember, EnumMemberDeclarationSyntax).Identifier.Text
                    )
                    
                    lMemberNode.FilePath = pCurrentFilePath
                    lMemberNode.StartLine = GetLineNumber(DirectCast(lMember, RoslynSyntaxNode))
                    lMemberNode.EndLine = lMemberNode.StartLine
                    
                    ' Extract initializer value if present
                    Dim lEnumMember = DirectCast(lMember, EnumMemberDeclarationSyntax)
                    If lEnumMember.Initializer IsNot Nothing Then
                        lMemberNode.InitialValue = lEnumMember.Initializer.Value.ToString()
                    End If
                    
                    lEnumNode.AddChild(lMemberNode)
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
                Dim lDelegateNode As New SimpleSyntaxNode(
                    CodeNodeType.eDelegate,
                    vDelegate.Identifier.Text
                )
                
                lDelegateNode.FilePath = pCurrentFilePath
                lDelegateNode.StartLine = GetLineNumber(DirectCast(vDelegate, RoslynSyntaxNode))
                lDelegateNode.EndLine = lDelegateNode.StartLine
                
                ' Extract modifiers
                ExtractModifiers(vDelegate.Modifiers, lDelegateNode)
                
                ' Determine if it's a function or sub
                ' Check the kind directly instead of trying to cast
                If vDelegate.Kind() = SyntaxKind.DelegateFunctionStatement Then
                    ' It's a function delegate, get the return type
                    If vDelegate.AsClause IsNot Nothing Then
                        lDelegateNode.ReturnType = vDelegate.AsClause.Type.ToString()
                    Else
                        lDelegateNode.ReturnType = "Object"
                    End If
                End If
                
                ' Extract parameters
                If vDelegate.ParameterList IsNot Nothing Then
                    for each lParam in vDelegate.ParameterList.Parameters
                        Dim lParamInfo As New ParameterInfo()
                        lParamInfo.Name = lParam.Identifier.Identifier.Text
                        lParamInfo.ParameterType = If(lParam.AsClause?.Type?.ToString(), "Object")
                        lParamInfo.IsOptional = lParam.Modifiers.Any(Function(m) m.Kind() = SyntaxKind.OptionalKeyword)
                        lParamInfo.IsByRef = lParam.Modifiers.Any(Function(m) m.Kind() = SyntaxKind.ByRefKeyword)
                        lParamInfo.IsParamArray = lParam.Modifiers.Any(Function(m) m.Kind() = SyntaxKind.ParamArrayKeyword)
                        
                        If lParam.Default IsNot Nothing Then
                            lParamInfo.DefaultValue = lParam.Default.Value.ToString()
                        End If
                        
                        lDelegateNode.Parameters.Add(lParamInfo)
                    Next
                End If
                
                ' Extract XML documentation
                ExtractXmlDocumentation(DirectCast(vDelegate, RoslynSyntaxNode), lDelegateNode)
                
                vParent.AddChild(lDelegateNode)
                
            Catch ex As Exception
                Console.WriteLine($"ProcessDelegate error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes overloaded operators
        ''' </summary>
        Private Sub ProcessOperator(vOperator As OperatorBlockSyntax, vParent As SimpleSyntaxNode)
            Try
                Dim lKind As CodeNodeType = CodeNodeType.eOperator
                Select Case vOperator.OperatorStatement.OperatorToken.Kind()
                    Case SyntaxKind.PlusToken
                        lKind = CodeNodeType.eOperator
                    Case SyntaxKind.MinusToken
                        lKind = CodeNodeType.eOperator
                    Case SyntaxKind.AsteriskToken
                        lKind = CodeNodeType.eOperator
                    Case SyntaxKind.SlashToken
                        lKind = CodeNodeType.eOperator
                    Case Else
                        lKind = CodeNodeType.eOperator
                End Select
                
                Dim lOperatorNode As New SimpleSyntaxNode(
                    lKind,
                    "Operator " & vOperator.OperatorStatement.OperatorToken.Text
                )
                
                lOperatorNode.FilePath = pCurrentFilePath
                lOperatorNode.StartLine = GetLineNumber(DirectCast(vOperator, RoslynSyntaxNode))
                lOperatorNode.EndLine = GetLineNumber(DirectCast(vOperator.EndOperatorStatement, RoslynSyntaxNode))
                
                ' Extract modifiers
                ExtractModifiers(vOperator.OperatorStatement.Modifiers, lOperatorNode)
                
                ' Extract return type
                lOperatorNode.ReturnType = If(vOperator.OperatorStatement.AsClause?.Type?.ToString(), "Object")
                
                ' Extract parameters
                If vOperator.OperatorStatement.ParameterList IsNot Nothing Then
                    for each lParam in vOperator.OperatorStatement.ParameterList.Parameters
                        Dim lParamInfo As New ParameterInfo()
                        lParamInfo.Name = lParam.Identifier.Identifier.Text
                        lParamInfo.ParameterType = If(lParam.AsClause?.Type?.ToString(), "Object")
                        
                        lOperatorNode.Parameters.Add(lParamInfo)
                    Next
                End If
                
                ' Extract XML documentation
                ExtractXmlDocumentation(DirectCast(vOperator, RoslynSyntaxNode), lOperatorNode)
                
                vParent.AddChild(lOperatorNode)
                
            Catch ex As Exception
                Console.WriteLine($"ProcessOperator error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes a method block (Sub/Function)
        ''' </summary>
        Private Sub ProcessMethodBlock(vMethod As MethodBlockSyntax, vParent As SimpleSyntaxNode)
            Try
                Dim lMethodNode As New SimpleSyntaxNode(
                    If(vMethod.SubOrFunctionStatement.Kind() = SyntaxKind.FunctionStatement, 
                       CodeNodeType.eFunction, 
                       CodeNodeType.eMethod),
                    vMethod.SubOrFunctionStatement.Identifier.Text
                )
                
                lMethodNode.FilePath = pCurrentFilePath
                lMethodNode.StartLine = GetLineNumber(DirectCast(vMethod, RoslynSyntaxNode))
                lMethodNode.EndLine = GetLineNumber(DirectCast(vMethod.EndSubOrFunctionStatement, RoslynSyntaxNode))
                
                ' Extract modifiers
                ExtractModifiers(vMethod.SubOrFunctionStatement.Modifiers, lMethodNode)
                
                ' Extract return type for functions
                If vMethod.SubOrFunctionStatement.Kind() = SyntaxKind.FunctionStatement Then
                    If vMethod.SubOrFunctionStatement.AsClause IsNot Nothing Then
                        lMethodNode.ReturnType = vMethod.SubOrFunctionStatement.AsClause.Type.ToString()
                    Else
                        lMethodNode.ReturnType = "Object"
                    End If
                End If
                
                ' Extract parameters
                ExtractParameters(vMethod.SubOrFunctionStatement.ParameterList, lMethodNode)
                
                ' Extract XML documentation
                ExtractXmlDocumentation(DirectCast(vMethod, RoslynSyntaxNode), lMethodNode)
                
                vParent.AddChild(lMethodNode)
                
            Catch ex As Exception
                Console.WriteLine($"ProcessMethodBlock error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes a constructor
        ''' </summary>
        Private Sub ProcessConstructor(vConstructor As ConstructorBlockSyntax, vParent As SimpleSyntaxNode)
            Try
                Dim lConstructorNode As New SimpleSyntaxNode(
                    CodeNodeType.eConstructor,
                    "New"
                )
                
                lConstructorNode.FilePath = pCurrentFilePath
                lConstructorNode.StartLine = GetLineNumber(DirectCast(vConstructor, RoslynSyntaxNode))
                lConstructorNode.EndLine = GetLineNumber(DirectCast(vConstructor.EndSubStatement, RoslynSyntaxNode))
                
                ' Extract modifiers
                ExtractModifiers(vConstructor.SubNewStatement.Modifiers, lConstructorNode)
                
                ' Extract parameters
                ExtractParameters(vConstructor.SubNewStatement.ParameterList, lConstructorNode)
                
                ' Extract XML documentation
                ExtractXmlDocumentation(DirectCast(vConstructor, RoslynSyntaxNode), lConstructorNode)
                
                vParent.AddChild(lConstructorNode)
                
            Catch ex As Exception
                Console.WriteLine($"ProcessConstructor error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes a property block (with Get/Set)
        ''' </summary>
        Private Sub ProcessPropertyBlock(vProperty As PropertyBlockSyntax, vParent As SimpleSyntaxNode)
            Try
                Dim lPropertyNode As New SimpleSyntaxNode(
                    CodeNodeType.eProperty,
                    vProperty.PropertyStatement.Identifier.Text
                )
                
                lPropertyNode.FilePath = pCurrentFilePath
                lPropertyNode.StartLine = GetLineNumber(DirectCast(vProperty, RoslynSyntaxNode))
                lPropertyNode.EndLine = GetLineNumber(DirectCast(vProperty.EndPropertyStatement, RoslynSyntaxNode))
                
                ' Extract modifiers
                ExtractModifiers(vProperty.PropertyStatement.Modifiers, lPropertyNode)
                
                ' Extract property type
                lPropertyNode.DataType = If(vProperty.PropertyStatement.AsClause?.Type?.ToString(), "Object")
                lPropertyNode.ReturnType = lPropertyNode.DataType
                
                ' Determine if it's read-only or write-only
                Dim lHasGetter As Boolean = False
                Dim lHasSetter As Boolean = False
                
                for each lAccessor in vProperty.Accessors
                    If lAccessor.Kind() = SyntaxKind.GetAccessorBlock Then
                        lHasGetter = True
                    ElseIf lAccessor.Kind() = SyntaxKind.SetAccessorBlock Then
                        lHasSetter = True
                    End If
                Next
                
                lPropertyNode.IsReadOnly = lHasGetter AndAlso Not lHasSetter
                lPropertyNode.IsWriteOnly = lHasSetter AndAlso Not lHasGetter
                
                ' Extract parameters (for indexed properties)
                ExtractParameters(vProperty.PropertyStatement.ParameterList, lPropertyNode)
                
                ' Extract XML documentation
                ExtractXmlDocumentation(DirectCast(vProperty, RoslynSyntaxNode), lPropertyNode)
                
                vParent.AddChild(lPropertyNode)
                
            Catch ex As Exception
                Console.WriteLine($"ProcessPropertyBlock error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes an auto-implemented property
        ''' </summary>
        Private Sub ProcessPropertyStatement(vProperty As PropertyStatementSyntax, vParent As SimpleSyntaxNode)
            Try
                Dim lPropertyNode As New SimpleSyntaxNode(
                    CodeNodeType.eProperty,
                    vProperty.Identifier.Text
                )
                
                lPropertyNode.FilePath = pCurrentFilePath
                lPropertyNode.StartLine = GetLineNumber(DirectCast(vProperty, RoslynSyntaxNode))
                lPropertyNode.EndLine = lPropertyNode.StartLine
                lPropertyNode.IsAutoImplemented = True
                
                ' Extract modifiers
                ExtractModifiers(vProperty.Modifiers, lPropertyNode)
                
                ' Extract property type
                lPropertyNode.DataType = If(vProperty.AsClause?.Type?.ToString(), "Object")
                lPropertyNode.ReturnType = lPropertyNode.DataType
                
                ' Extract initializer
                If vProperty.Initializer IsNot Nothing Then
                    lPropertyNode.InitialValue = vProperty.Initializer.Value.ToString()
                End If
                
                ' Extract parameters (for indexed properties)
                ExtractParameters(vProperty.ParameterList, lPropertyNode)
                
                ' Extract XML documentation
                ExtractXmlDocumentation(DirectCast(vProperty, RoslynSyntaxNode), lPropertyNode)
                
                vParent.AddChild(lPropertyNode)
                
            Catch ex As Exception
                Console.WriteLine($"ProcessPropertyStatement error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes an event block
        ''' </summary>
        Private Sub ProcessEventBlock(vEvent As EventBlockSyntax, vParent As SimpleSyntaxNode)
            Try
                Dim lEventNode As New SimpleSyntaxNode(
                    CodeNodeType.eEvent,
                    vEvent.EventStatement.Identifier.Text
                )
                
                lEventNode.FilePath = pCurrentFilePath
                lEventNode.StartLine = GetLineNumber(DirectCast(vEvent, RoslynSyntaxNode))
                lEventNode.EndLine = GetLineNumber(DirectCast(vEvent.EndEventStatement, RoslynSyntaxNode))
                
                ' Extract modifiers
                ExtractModifiers(vEvent.EventStatement.Modifiers, lEventNode)
                
                ' Extract event type/signature
                If vEvent.EventStatement.AsClause IsNot Nothing Then
                    lEventNode.DataType = vEvent.EventStatement.AsClause.Type.ToString()
                End If
                
                ' Extract parameters
                ExtractParameters(vEvent.EventStatement.ParameterList, lEventNode)
                
                ' Extract XML documentation
                ExtractXmlDocumentation(DirectCast(vEvent, RoslynSyntaxNode), lEventNode)
                
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
                Dim lEventNode As New SimpleSyntaxNode(
                    CodeNodeType.eEvent,
                    vEvent.Identifier.Text
                )
                
                lEventNode.FilePath = pCurrentFilePath
                lEventNode.StartLine = GetLineNumber(DirectCast(vEvent, RoslynSyntaxNode))
                lEventNode.EndLine = lEventNode.StartLine
                
                ' Extract modifiers
                ExtractModifiers(vEvent.Modifiers, lEventNode)
                
                ' Extract event type/signature
                If vEvent.AsClause IsNot Nothing Then
                    lEventNode.DataType = vEvent.AsClause.Type.ToString()
                End If
                
                ' Extract parameters
                ExtractParameters(vEvent.ParameterList, lEventNode)
                
                ' Extract XML documentation
                ExtractXmlDocumentation(DirectCast(vEvent, RoslynSyntaxNode), lEventNode)
                
                vParent.AddChild(lEventNode)
                
            Catch ex As Exception
                Console.WriteLine($"ProcessEventStatement error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes field declarations
        ''' </summary>
        Private Sub ProcessField(vField As FieldDeclarationSyntax, vParent As SimpleSyntaxNode)
            Try
                ' Extract modifiers once for all declarators
                Dim lModifiers = vField.Modifiers
                
                for each lDeclarator in vField.Declarators
                    for each lName in lDeclarator.Names
                        Dim lFieldNode As New SimpleSyntaxNode(
                            CodeNodeType.eField,
                            lName.Identifier.Text
                        )
                        
                        lFieldNode.FilePath = pCurrentFilePath
                        lFieldNode.StartLine = GetLineNumber(DirectCast(vField, RoslynSyntaxNode))
                        lFieldNode.EndLine = lFieldNode.StartLine
                        
                        ' Extract modifiers
                        ExtractModifiers(lModifiers, lFieldNode)
                        
                        ' Extract field type
                        lFieldNode.DataType = If(lDeclarator.AsClause?.Type?.ToString(), "Object")
                        
                        ' Extract initializer
                        If lDeclarator.Initializer IsNot Nothing Then
                            lFieldNode.InitialValue = lDeclarator.Initializer.Value.ToString()
                        End If
                        
                        ' Extract XML documentation
                        ExtractXmlDocumentation(DirectCast(vField, RoslynSyntaxNode), lFieldNode)
                        
                        vParent.AddChild(lFieldNode)
                    Next
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ProcessField error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Helper Methods =====
        
        ''' <summary>
        ''' Extracts modifiers and sets appropriate flags
        ''' </summary>
        Private Sub ExtractModifiers(vModifiers As SyntaxTokenList, vNode As SimpleSyntaxNode)
            Try
                for each lModifier in vModifiers
                    Select Case lModifier.Kind()
                        Case SyntaxKind.PublicKeyword
                            vNode.IsPublic = True
                            vNode.Visibility = SimpleSyntaxNode.eVisibility.ePublic
                        Case SyntaxKind.PrivateKeyword
                            vNode.IsPrivate = True
                            vNode.Visibility = SimpleSyntaxNode.eVisibility.ePrivate
                        Case SyntaxKind.ProtectedKeyword
                            vNode.IsProtected = True
                            vNode.Visibility = SimpleSyntaxNode.eVisibility.eProtected
                        Case SyntaxKind.FriendKeyword
                            vNode.IsFriend = True
                            vNode.Visibility = SimpleSyntaxNode.eVisibility.eFriend
                        Case SyntaxKind.SharedKeyword
                            vNode.IsShared = True
                        Case SyntaxKind.OverridableKeyword
                            vNode.IsOverridable = True
                        Case SyntaxKind.OverridesKeyword
                            vNode.IsOverrides = True
                        Case SyntaxKind.MustOverrideKeyword
                            vNode.IsMustOverride = True
                        Case SyntaxKind.NotOverridableKeyword
                            vNode.IsNotOverridable = True
                        Case SyntaxKind.MustInheritKeyword
                            vNode.IsMustInherit = True
                        Case SyntaxKind.NotInheritableKeyword
                            vNode.IsNotInheritable = True
                        Case SyntaxKind.ReadOnlyKeyword
                            vNode.IsReadOnly = True
                        Case SyntaxKind.WriteOnlyKeyword
                            vNode.IsWriteOnly = True
                        Case SyntaxKind.ConstKeyword
                            vNode.IsConst = True
                        Case SyntaxKind.ShadowsKeyword
                            vNode.IsShadows = True
                        Case SyntaxKind.AsyncKeyword
                            vNode.IsAsync = True
                        Case SyntaxKind.IteratorKeyword
                            vNode.IsIterator = True
                        Case SyntaxKind.WithEventsKeyword
                            vNode.IsWithEvents = True
                        Case SyntaxKind.PartialKeyword
                            vNode.IsPartial = True
                    End Select
                Next
                
                ' Default visibility if none specified
                If vNode.Visibility = SimpleSyntaxNode.eVisibility.eUnspecified Then
                    vNode.Visibility = SimpleSyntaxNode.eVisibility.ePublic
                    vNode.IsPublic = True
                End If
                
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
                    lParamInfo.ParameterType = If(lParam.AsClause?.Type?.ToString(), "Object")
                    lParamInfo.IsOptional = lParam.Modifiers.Any(Function(m) m.Kind() = SyntaxKind.OptionalKeyword)
                    lParamInfo.IsByRef = lParam.Modifiers.Any(Function(m) m.Kind() = SyntaxKind.ByRefKeyword)
                    lParamInfo.IsParamArray = lParam.Modifiers.Any(Function(m) m.Kind() = SyntaxKind.ParamArrayKeyword)
                    
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
        ''' Extracts XML documentation comments
        ''' </summary>
        Private Sub ExtractXmlDocumentation(vNode As RoslynSyntaxNode, vTargetNode As SimpleSyntaxNode)
            Try
                If vNode Is Nothing Then Return
                
                Dim lTrivia = vNode.GetLeadingTrivia()
                Dim lXmlDoc As New XmlDocInfo()
                
                for each lTriviaItem in lTrivia
                    If lTriviaItem.Kind() = SyntaxKind.DocumentationCommentExteriorTrivia Then
                        Dim lStructure = lTriviaItem.GetStructure()
                        If TypeOf lStructure Is DocumentationCommentTriviaSyntax Then
                            Dim lDocComment = DirectCast(lStructure, DocumentationCommentTriviaSyntax)
                            ParseXmlDocumentation(lDocComment, lXmlDoc)
                        End If
                    End If
                Next
                
                ' Only assign if we found documentation
                If Not String.IsNullOrEmpty(lXmlDoc.Summary) OrElse 
                   lXmlDoc.Parameters.Count > 0 OrElse
                   Not String.IsNullOrEmpty(lXmlDoc.Returns) Then
                    vTargetNode.XmlDocumentation = lXmlDoc
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ExtractXmlDocumentation error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Parses XML documentation structure
        ''' </summary>
        Private Sub ParseXmlDocumentation(vDocComment As DocumentationCommentTriviaSyntax, vXmlDoc As XmlDocInfo)
            Try
                for each lNode in vDocComment.Content
                    If TypeOf lNode Is XmlElementSyntax Then
                        Dim lElement = DirectCast(lNode, XmlElementSyntax)
                        ' Fix: Check if StartTag and Name exist before accessing
                        If lElement.StartTag IsNot Nothing AndAlso lElement.StartTag.Name IsNot Nothing Then
                            Dim lTagName As String = ""
                            
                            ' Handle different types of XML name syntax
                            If TypeOf lElement.StartTag.Name Is XmlNameSyntax Then
                                Dim lXmlName = DirectCast(lElement.StartTag.Name, XmlNameSyntax)
                                lTagName = lXmlName.LocalName.Text.ToLower()
                            ElseIf TypeOf lElement.StartTag.Name Is XmlPrefixNameSyntax Then
                                ' XmlPrefixNameSyntax doesn't have LocalName property
                                ' Use ToString to get the full name
                                lTagName = lElement.StartTag.Name.ToString().ToLower()
                                ' Remove any prefix if present
                                Dim lColonIndex = lTagName.IndexOf(":"c)
                                If lColonIndex >= 0 Then
                                    lTagName = lTagName.Substring(lColonIndex + 1)
                                End If
                            Else
                                ' Fallback: use ToString
                                lTagName = lElement.StartTag.Name.ToString().ToLower()
                            End If
                            
                            Select Case lTagName
                                Case "summary"
                                    vXmlDoc.Summary = GetXmlElementContent(lElement)
                                    
                                Case "param"
                                    ' Get parameter name from attribute
                                    Dim lParamName As String = ""
                                    for each lAttr in lElement.StartTag.Attributes
                                        If TypeOf lAttr Is XmlNameAttributeSyntax Then
                                            Dim lNameAttr = DirectCast(lAttr, XmlNameAttributeSyntax)
                                            ' XmlNameAttributeSyntax has a Name property which is an XmlNameSyntax
                                            If lNameAttr.Name IsNot Nothing Then
                                                If TypeOf lNameAttr.Name Is XmlNameSyntax Then
                                                    Dim lXmlName = DirectCast(lNameAttr.Name, XmlNameSyntax)
                                                    lParamName = lXmlName.LocalName.Text
                                                Else
                                                    ' Fallback to ToString
                                                    lParamName = lNameAttr.Name.ToString()
                                                End If
                                            End If
                                            Exit for
                                        End If
                                    Next
                                    
                                    If Not String.IsNullOrEmpty(lParamName) Then
                                        vXmlDoc.Parameters(lParamName) = GetXmlElementContent(lElement)
                                    End If
                                    
                                Case "typeparam"
                                    ' Get type parameter name from attribute
                                    Dim lTypeParamName As String = ""
                                    for each lAttr in lElement.StartTag.Attributes
                                        If TypeOf lAttr Is XmlNameAttributeSyntax Then
                                            Dim lNameAttr = DirectCast(lAttr, XmlNameAttributeSyntax)
                                            ' XmlNameAttributeSyntax has a Name property which is an XmlNameSyntax
                                            If lNameAttr.Name IsNot Nothing Then
                                                If TypeOf lNameAttr.Name Is XmlNameSyntax Then
                                                    Dim lXmlName = DirectCast(lNameAttr.Name, XmlNameSyntax)
                                                    lTypeParamName = lXmlName.LocalName.Text
                                                Else
                                                    ' Fallback to ToString
                                                    lTypeParamName = lNameAttr.Name.ToString()
                                                End If
                                            End If
                                            Exit for
                                        End If
                                    Next
                                    
                                    If Not String.IsNullOrEmpty(lTypeParamName) Then
                                        vXmlDoc.Parameters(lTypeParamName) = GetXmlElementContent(lElement)
                                    End If
                                    
                                Case "returns"
                                    vXmlDoc.Returns = GetXmlElementContent(lElement)
                                    
                                Case "remarks"
                                    vXmlDoc.Remarks = GetXmlElementContent(lElement)
                                    
                                Case "example"
                                    vXmlDoc.Example = GetXmlElementContent(lElement)
                                    
                                Case "exception"
                                    ' Get exception type from cref attribute
                                    Dim lExceptionType As String = ""
                                    for each lAttr in lElement.StartTag.Attributes
                                        If TypeOf lAttr Is XmlCrefAttributeSyntax Then
                                            Dim lCrefAttr = DirectCast(lAttr, XmlCrefAttributeSyntax)
                                            lExceptionType = lCrefAttr.Reference.ToString()
                                            Exit for
                                        End If
                                    Next
                                    
                                    If Not String.IsNullOrEmpty(lExceptionType) Then
                                        vXmlDoc.Exceptions(lExceptionType) = GetXmlElementContent(lElement)
                                    End If
                                    
                                Case "seealso"
                                    ' Get cref reference
                                    for each lAttr in lElement.StartTag.Attributes
                                        If TypeOf lAttr Is XmlCrefAttributeSyntax Then
                                            Dim lCrefAttr = DirectCast(lAttr, XmlCrefAttributeSyntax)
                                            vXmlDoc.SeeAlso.Add(lCrefAttr.Reference.ToString())
                                            Exit for
                                        End If
                                    Next
                                    
                                Case "value"
                                    vXmlDoc.Value = GetXmlElementContent(lElement)
                            End Select
                        End If
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ParseXmlDocumentation error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Gets the text content of an XML element
        ''' </summary>
        Private Function GetXmlElementContent(vElement As XmlElementSyntax) As String
            Try
                Dim lContent As New Text.StringBuilder()
                
                for each lNode in vElement.Content
                    If TypeOf lNode Is XmlTextSyntax Then
                        Dim lText = DirectCast(lNode, XmlTextSyntax)
                        for each lToken in lText.TextTokens
                            lContent.Append(lToken.ToString())
                        Next
                    ElseIf TypeOf lNode Is XmlElementSyntax Then
                        ' Recursively get content of nested elements
                        lContent.Append(GetXmlElementContent(DirectCast(lNode, XmlElementSyntax)))
                    End If
                Next
                
                ' Clean up the content
                Dim lResult = lContent.ToString().Trim()
                lResult = System.Text.RegularExpressions.Regex.Replace(lResult, "\s+", " ")
                Return lResult
                
            Catch ex As Exception
                Console.WriteLine($"GetXmlElementContent error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ''' <summary>
        ''' Gets the line number for a syntax node
        ''' </summary>
        Private Function GetLineNumber(vNode As RoslynSyntaxNode) As Integer
            Try
                If vNode Is Nothing Then Return 0
                
                Dim lLocation = vNode.GetLocation()
                If lLocation IsNot Nothing Then
                    Dim lLineSpan = lLocation.GetLineSpan()
                    Return lLineSpan.StartLinePosition.Line
                End If
                
                Return 0
                
            Catch ex As Exception
                Console.WriteLine($"GetLineNumber error: {ex.Message}")
                Return 0
            End Try
        End Function
        
    End Class
    
End Namespace