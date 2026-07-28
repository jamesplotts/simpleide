' Utilities/ReflectionHelper.vb - Reflection-based help URL generation
Imports System
Imports System.Reflection
Imports System.Linq

Namespace Utilities
    Public Class ReflectionHelper
        
        ''' <summary>
        ''' Finds a Type by name across all currently loaded assemblies (BCL, GTK#, and
        ''' anything else the process has referenced)
        ''' </summary>
        ''' <param name="vTypeName">Type name to search for - full name (e.g. "Gtk.Orientation")
        ''' or, if it happens to match a top-level type in some loaded assembly, a bare name</param>
        ''' <returns>The matching Type, or Nothing if no loaded assembly has it</returns>
        ''' <remarks>
        ''' Friend (not Private) so callers outside this class - e.g.
        ''' CustomDrawingEditor.EnumParameterHint.vb's system-Enum lookup - can reuse this
        ''' rather than re-implementing the same loaded-assemblies scan
        ''' </remarks>
        Friend Shared Function FindTypeByName(vTypeName As String) As Type
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
        
    End Class
End Namespace
