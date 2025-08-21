' Utilities/ReflectionHelper.vb - Reflection-based help URL generation
Imports System
Imports System.Reflection
Imports System.Linq

Namespace Utilities
    Public Class ReflectionHelper
        
        ' Type information structure
        Public Structure TypeInfo
            Public FullName As String
            Public TypeNamespace As String  ' Changed from Namespace to TypeNamespace
            Public Name As String
            Public AssemblyName As String   ' Changed from Assembly to AssemblyName
            Public AssemblyVersion As String
            Public IsGtk As Boolean
            Public IsDotNet As Boolean
            Public DocumentationUrl As String
        End Structure
        
        ' Get type information from a type name
        Public Shared Function GetTypeInfo(vTypeName As String) As TypeInfo?
            Try
                ' Try to find the type in loaded assemblies
                Dim lType As Type = Nothing
                
                ' First try exact match
                lType = FindTypeByName(vTypeName)
                
                ' If not found, try common namespaces
                If lType Is Nothing Then
                    Dim lCommonNamespaces() As String = {
                        "System", "System.IO", "System.Collections.Generic",
                        "System.Linq", "System.Text", "System.Threading.Tasks",
                        "Gtk", "Gdk", "GLib", "Pango", "Cairo",
                        "Microsoft.VisualBasic", "SimpleIDE", "SimpleIDE.Models",
                        "SimpleIDE.UI", "SimpleIDE.Utilities", "SimpleIDE.Widgets"
                    }
                    
                    For Each lNamespace In lCommonNamespaces
                        lType = FindTypeByName($"{lNamespace}.{vTypeName}")
                        If lType IsNot Nothing Then Exit For
                    Next
                End If
                
                If lType Is Nothing Then Return Nothing
                
                ' Build type info
                Dim lInfo As New TypeInfo With {
                    .FullName = lType.FullName,
                    .TypeNamespace = lType.Namespace,
                    .Name = lType.Name,
                    .AssemblyName = lType.Assembly.GetName().Name,
                    .AssemblyVersion = lType.Assembly.GetName().Version.ToString(),
                    .IsGtk = IsGtkType(lType),
                    .IsDotNet = IsDotNetType(lType)
                }
                
                ' Generate documentation URL
                lInfo.DocumentationUrl = GenerateDocumentationUrl(lInfo)
                
                Return lInfo
                
            Catch ex As Exception
                Console.WriteLine($"error getting Type info for '{vTypeName}': {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ' Find type by name in loaded assemblies
        Private Shared Function FindTypeByName(vTypeName As String) As Type
            Try
                ' Check all loaded assemblies
                For Each lAssembly In AppDomain.CurrentDomain.GetAssemblies()
                    Try
                        Dim lType As Type = lAssembly.GetType(vTypeName, False, True)
                        If lType IsNot Nothing Then Return lType
                    Catch
                        ' Skip assemblies that can't be searched
                    End Try
                Next
                
                ' Also try Type.GetType which handles mscorlib types
                Return Type.GetType(vTypeName, False, True)
                
            Catch ex As Exception
                Console.WriteLine($"error finding Type '{vTypeName}': {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ' Check if type is from GTK
        Private Shared Function IsGtkType(vType As Type) As Boolean
            Dim lAssemblyName As String = vType.Assembly.GetName().Name.ToLower()
            Return lAssemblyName.Contains("gtk") OrElse 
                   lAssemblyName.Contains("gdk") OrElse
                   lAssemblyName.Contains("glib") OrElse
                   lAssemblyName.Contains("gio") OrElse
                   lAssemblyName.Contains("pango") OrElse
                   lAssemblyName.Contains("cairo")
        End Function
        
        ' Check if type is from .NET
        Private Shared Function IsDotNetType(vType As Type) As Boolean
            Dim lAssemblyName As String = vType.Assembly.GetName().Name.ToLower()
            Return lAssemblyName.StartsWith("system") OrElse
                   lAssemblyName.StartsWith("microsoft") OrElse
                   lAssemblyName = "mscorlib" OrElse
                   lAssemblyName = "netstandard"
        End Function
        
        ' Generate documentation URL based on type info
        Private Shared Function GenerateDocumentationUrl(vInfo As TypeInfo) As String
            Try
                If vInfo.IsGtk Then
                    ' GTK documentation
                    Dim lGtkVersion As String = "gtk3"
                    Dim lClassName As String = vInfo.Name
                    
                    ' Handle special cases
                    Select Case vInfo.TypeNamespace?.ToLower()
                        Case "gdk"
                            Return $"https://docs.gtk.org/gdk3/class.{lClassName}.html"
                        Case "glib"
                            Return $"https://docs.gtk.org/glib/struct.{lClassName}.html"
                        Case "pango"
                            Return $"https://docs.gtk.org/Pango/class.{lClassName}.html"
                        Case Else
                            ' Default GTK
                            Return $"https://docs.gtk.org/{lGtkVersion}/class.{lClassName}.html"
                    End Select
                    
                ElseIf vInfo.IsDotNet Then
                    ' .NET documentation
                    Dim lNamespace As String = vInfo.TypeNamespace?.ToLower()
                    Dim lFullName As String = vInfo.FullName?.ToLower()
                    
                    ' Handle special namespaces
                    If lNamespace?.StartsWith("microsoft.visualbasic") Then
                        Return $"https://learn.microsoft.com/en-us/dotnet/api/{lFullName}?view=net-8.0"
                    Else
                        Return $"https://learn.microsoft.com/en-us/dotnet/api/{lFullName}?view=net-8.0"
                    End If
                    
                Else
                    ' Unknown type - search
                    Return $"https://learn.microsoft.com/en-us/search/?terms={vInfo.Name}"
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error generating documentation Url: {ex.Message}")
                Return $"https://learn.microsoft.com/en-us/search/?terms={vInfo.Name}"
            End Try
        End Function
        
        ' Get type information from a Type object
        Public Shared Function GetTypeInfo(vType As Type) As TypeInfo
            Try
                Dim lInfo As New TypeInfo With {
                    .FullName = vType.FullName,
                    .TypeNamespace = vType.Namespace,
                    .Name = vType.Name,
                    .AssemblyName = vType.Assembly.GetName().Name,
                    .AssemblyVersion = vType.Assembly.GetName().Version.ToString(),
                    .IsGtk = IsGtkType(vType),
                    .IsDotNet = IsDotNetType(vType)
                }
                
                ' Generate documentation URL
                lInfo.DocumentationUrl = GenerateDocumentationUrl(lInfo)
                
                Return lInfo
                
            Catch ex As Exception
                Console.WriteLine($"error getting Type info: {ex.Message}")
                Return New TypeInfo()
            End Try
        End Function
        
        ' Get all available types for CodeSense
        Public Shared Function GetAvailableTypes(Optional vFilter As String = "") As List(Of String)
            Dim lTypes As New List(Of String)
            
            Try
                ' Get types from key assemblies
                Dim lKeyAssemblies() As String = {
                    "mscorlib", "System", "System.Core", "System.Linq",
                    "gtk-sharp", "gdk-sharp", "glib-sharp", "gio-sharp",
                    GetType(ReflectionHelper).Assembly.GetName().Name ' SimpleIDE
                }
                
                For Each lAssembly In AppDomain.CurrentDomain.GetAssemblies()
                    Try
                        Dim lAssemblyName As String = lAssembly.GetName().Name
                        
                        ' Only process key assemblies
                        If Not lKeyAssemblies.any(Function(k) lAssemblyName.StartsWith(k, StringComparison.OrdinalIgnoreCase)) Then
                            Continue For
                        End If
                        
                        For Each lType In lAssembly.GetExportedTypes()
                            If Not String.IsNullOrEmpty(vFilter) AndAlso 
                               Not lType.Name.StartsWith(vFilter, StringComparison.OrdinalIgnoreCase) Then
                                Continue For
                            End If
                            
                            ' Add both short and full names
                            lTypes.Add(lType.Name)
                            If Not String.IsNullOrEmpty(lType.Namespace) Then
                                lTypes.Add(lType.FullName)
                            End If
                        Next
                    Catch
                        ' Skip assemblies that can't be enumerated
                    End Try
                Next
                
                ' Remove duplicates and sort
                Return lTypes.Distinct().OrderBy(Function(t) t).ToList()
                
            Catch ex As Exception
                Console.WriteLine($"error getting available types: {ex.Message}")
                Return lTypes
            End Try
        End Function
        
        ' Get help URL for a keyword
        Public Shared Function GetHelpUrl(vKeyword As String) As String
            Try
                ' First try to get type info
                Dim lTypeInfo As TypeInfo? = GetTypeInfo(vKeyword)
                If lTypeInfo.HasValue Then
                    Return lTypeInfo.Value.DocumentationUrl
                End If
                
                ' If not a type, check for VB keywords
                Dim lVbKeywords As New Dictionary(Of String, String) From {
                    {"dim", "statements/dim-statement"},
                    {"if", "statements/if-then-else-statement"},
                    {"for", "statements/for-next-statement"},
                    {"while", "statements/while-end-while-statement"},
                    {"select", "statements/select-case-statement"},
                    {"function", "statements/function-statement"},
                    {"sub", "statements/sub-statement"},
                    {"class", "statements/class-statement"},
                    {"module", "statements/module-statement"},
                    {"imports", "statements/imports-statement"},
                    {"namespace", "statements/namespace-statement"},
                    {"public", "Modifiers/public"},
                    {"private", "Modifiers/private"},
                    {"protected", "Modifiers/protected"},
                    {"shared", "Modifiers/shared"},
                    {"readonly", "Modifiers/readonly"},
                    {"withevents", "Modifiers/withevents"}
                }
                
                Dim lLowerKeyword As String = vKeyword.ToLower()
                If lVbKeywords.ContainsKey(lLowerKeyword) Then
                    Return $"https://learn.microsoft.com/en-us/dotnet/Visual-basic/Language-Reference/{lVbKeywords(lLowerKeyword)}"
                End If
                
                ' Default to search
                Return $"https://learn.microsoft.com/en-us/search/?terms={vKeyword}+Visual+basic"
                
            Catch ex As Exception
                Console.WriteLine($"error getting help Url: {ex.Message}")
                Return $"https://learn.microsoft.com/en-us/search/?terms={vKeyword}"
            End Try
        End Function
        
    End Class
End Namespace
