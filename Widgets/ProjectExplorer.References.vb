' Utilities/ProjectExplorer.References.vb - Fixed version with correct TreeStore column types and namespace imports
Imports Gtk
Imports System.IO
Imports System.Linq
Imports SimpleIDE.Utilities
Imports SimpleIDE.Models
Imports SimpleIDE.Managers

Namespace Widgets
    
    Partial Public Class ProjectExplorer
        
        ' Events
        Public Event ReferencesChanged()
        
        ' Reference node constants
        Private Const REFERENCES_NODE_NAME As String = "References"
        Private pReferencesIter As TreeIter
        Private pHasReferencesNode As Boolean = False
        
        ' Add reference management items to context menu
        Private Sub AddReferenceContextMenuItems()
            Try
                ' Find position to insert (after "Add New Folder" item)
                Dim lInsertPos As Integer = 3
                For i As Integer = 0 To pContextMenu.Children.Length - 1
                    Dim lItem As Widget = pContextMenu.Children(i)
                    If TypeOf lItem Is MenuItem Then
                        Dim lMenuItem As MenuItem = CType(lItem, MenuItem)
                        If lMenuItem.Label.Contains("Add New Folder") Then
                            lInsertPos = i + 1
                            Exit For
                        End If
                    End If
                Next
                
                ' Add separator
                Dim lSeparator As New SeparatorMenuItem()
                lSeparator.Name = "ReferenceSeparator"
                lSeparator.NoShowAll = True
                pContextMenu.Insert(lSeparator, lInsertPos)
                
                ' Add Reference submenu
                Dim lAddReferenceMenu As New Menu()
                Dim lAddReferenceMenuItem As New MenuItem("Add Reference")
                lAddReferenceMenuItem.Name = "AddReferenceMenu"
                lAddReferenceMenuItem.Submenu = lAddReferenceMenu
                lAddReferenceMenuItem.NoShowAll = True
                
                ' Add Assembly Reference
                Dim lAddAssemblyItem As New MenuItem("Add Assembly...")
                AddHandler lAddAssemblyItem.Activated, AddressOf OnAddAssemblyReference
                lAddReferenceMenu.Append(lAddAssemblyItem)
                
                ' Add NuGet Package
                Dim lAddNuGetItem As New MenuItem("Add NuGet Package...")
                AddHandler lAddNuGetItem.Activated, AddressOf OnAddNuGetPackage
                lAddReferenceMenu.Append(lAddNuGetItem)
                
                ' Add Project Reference
                Dim lAddProjectItem As New MenuItem("Add project Reference...")
                AddHandler lAddProjectItem.Activated, AddressOf OnAddProjectReference
                lAddReferenceMenu.Append(lAddProjectItem)
                
                pContextMenu.Insert(lAddReferenceMenuItem, lInsertPos + 1)
                
                ' Manage References
                Dim lManageReferencesItem As New MenuItem("Manage References...")
                lManageReferencesItem.Name = "ManageReferences"
                lManageReferencesItem.NoShowAll = True
                AddHandler lManageReferencesItem.Activated, AddressOf OnManageReferences
                pContextMenu.Insert(lManageReferencesItem, lInsertPos + 2)
                
                ' Remove Reference
                Dim lRemoveReferenceItem As New MenuItem("Remove Reference")
                lRemoveReferenceItem.Name = "RemoveReference"
                lRemoveReferenceItem.NoShowAll = True
                AddHandler lRemoveReferenceItem.Activated, AddressOf OnRemoveReference
                pContextMenu.Insert(lRemoveReferenceItem, lInsertPos + 3)
                
                pContextMenu.ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"error adding Reference Context menu items: {ex.Message}")
            End Try
        End Sub
        
        ' Update context menu based on selected node
        Private Sub UpdateReferenceContextMenu()
            Try
                ' Get menu items
                Dim lReferenceSeparator As Widget = Nothing
                Dim lAddReferenceMenu As Widget = Nothing
                Dim lManageReferences As Widget = Nothing
                Dim lRemoveReference As Widget = Nothing
                
                For Each lChild In pContextMenu.Children
                    If lChild.Name = "ReferenceSeparator" Then lReferenceSeparator = lChild
                    If lChild.Name = "AddReferenceMenu" Then lAddReferenceMenu = lChild
                    If lChild.Name = "ManageReferences" Then lManageReferences = lChild
                    If lChild.Name = "RemoveReference" Then lRemoveReference = lChild
                Next
                
                ' Check if current selection is on References node or a reference
                Dim lIsReferencesNode As Boolean = (pCurrentSelectedPath = REFERENCES_NODE_NAME)
                Dim lIsReference As Boolean = IsReferenceNode()
                
                ' Show/hide reference items
                If lReferenceSeparator IsNot Nothing Then
                    lReferenceSeparator.Visible = lIsReferencesNode OrElse lIsReference
                End If
                
                If lAddReferenceMenu IsNot Nothing Then
                    lAddReferenceMenu.Visible = lIsReferencesNode
                End If
                
                If lManageReferences IsNot Nothing Then
                    lManageReferences.Visible = lIsReferencesNode
                End If
                
                If lRemoveReference IsNot Nothing Then
                    lRemoveReference.Visible = lIsReference
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error updating Reference Context menu: {ex.Message}")
            End Try
        End Sub
        
        ' Create References node
        Public Sub CreateReferencesNode()
            Try
                If pHasReferencesNode OrElse pTreeStore Is Nothing Then
                    Return
                End If
                
                ' Get root iter
                Dim lRootIter As TreeIter
                If Not pTreeStore.GetIterFirst(lRootIter) Then
                    Return
                End If
                
                ' Add References node (using column types: String, String, Boolean, String)
                pReferencesIter = pTreeStore.AppendValues(lRootIter, REFERENCES_NODE_NAME, "", False, "References")
                pHasReferencesNode = True
                
                ' Load existing references
                LoadProjectReferences()
                
            Catch ex As Exception
                Console.WriteLine($"error creating References Node: {ex.Message}")
            End Try
        End Sub
        
        ' Load project references
        Private Sub LoadProjectReferences()
            Try
                If Not pHasReferencesNode OrElse String.IsNullOrEmpty(pProjectFile) Then
                    Return
                End If
                
                ' Clear existing reference nodes
                Dim lChildIter As TreeIter
                If pTreeStore.IterChildren(lChildIter, pReferencesIter) Then
                    While pTreeStore.Remove(lChildIter)
                        ' Keep removing until no more children
                    End While
                End If
                
                ' Get references from project file
                Dim lReferenceManager As New ReferenceManager()
                Dim lReferences As List(Of ReferenceManager.ReferenceInfo) = lReferenceManager.GetAllReferences(pProjectFile)
                
                ' Separate by type
                Dim lAssemblies As New List(Of ReferenceManager.ReferenceInfo)
                Dim lPackages As New List(Of ReferenceManager.ReferenceInfo)
                Dim lProjects As New List(Of ReferenceManager.ReferenceInfo)
                
                For Each lRef In lReferences
                    Select Case lRef.Type
                        Case ReferenceManager.ReferenceType.eAssembly
                            lAssemblies.Add(lRef)
                        Case ReferenceManager.ReferenceType.ePackage
                            lPackages.Add(lRef)
                        Case ReferenceManager.ReferenceType.eProject
                            lProjects.Add(lRef)
                    End Select
                Next
                
                ' Add assemblies (now using correct column types)
                If lAssemblies.Count > 0 Then
                    For Each lRef In lAssemblies.OrderBy(Function(r) r.Name)
                        Dim lDisplayName As String = lRef.Name
                        If Not String.IsNullOrEmpty(lRef.Path) Then
                            lDisplayName &= " (" & System.IO.Path.GetFileName(lRef.Path) & ")"
                        End If
                        
                        ' Append with correct column types: String, String, Boolean, String
                        pTreeStore.AppendValues(pReferencesIter,
                            lDisplayName,                       ' Column 0: String (Name)
                            $"Assembly|{lRef.Name}",           ' Column 1: String (full Path/Identifier)
                            False,                             ' Column 2: Boolean (is file)
                            "assembly"                         ' Column 3: String (Node Type)
                        )
                    Next
                End If
                
                ' Add packages
                If lPackages.Count > 0 Then
                    For Each lRef In lPackages.OrderBy(Function(r) r.Name)
                        Dim lDisplayName As String = $"{lRef.Name} {lRef.Version}"
                        
                        pTreeStore.AppendValues(pReferencesIter,
                            lDisplayName,                      ' Column 0: String (Name)
                            $"Package|{lRef.Name}",           ' Column 1: String (full Path/Identifier)
                            False,                            ' Column 2: Boolean (is file)
                            "package"                         ' Column 3: String (Node Type)
                        )
                    Next
                End If
                
                ' Add project references
                If lProjects.Count > 0 Then
                    For Each lRef In lProjects.OrderBy(Function(r) r.Name)
                        pTreeStore.AppendValues(pReferencesIter,
                            lRef.Name,                        ' Column 0: String (Name)
                            $"project|{lRef.Path}",          ' Column 1: String (full Path/Identifier)
                            False,                           ' Column 2: Boolean (is file)
                            "projectref"                     ' Column 3: String (Node Type)
                        )
                    Next
                End If
                
                ' Expand References node
                Dim lPath As TreePath = pTreeStore.GetPath(pReferencesIter)
                pTreeView.ExpandRow(lPath, False)
                
            Catch ex As Exception
                Console.WriteLine($"error loading project References: {ex.Message}")
            End Try
        End Sub
        
        ' Check if current selection is a reference node
        Private Function IsReferenceNode() As Boolean
            Try
                If String.IsNullOrEmpty(pCurrentSelectedPath) Then Return False
                
                ' Check if path contains reference type indicator
                Return pCurrentSelectedPath.Contains("assembly|") OrElse 
                       pCurrentSelectedPath.Contains("package|") OrElse 
                       pCurrentSelectedPath.Contains("project|")
                
            Catch ex As Exception
                Console.WriteLine($"error checking Reference Node: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Get reference info from selected node
        Private Function GetSelectedReferenceInfo() As ReferenceManager.ReferenceInfo
            Try
                If Not IsReferenceNode() Then Return Nothing
                
                Dim lParts() As String = pCurrentSelectedPath.Split("|"c)
                If lParts.Length < 2 Then Return Nothing
                
                Dim lRefInfo As New ReferenceManager.ReferenceInfo()
                
                Select Case lParts(0)
                    Case "Assembly"
                        lRefInfo.Type = ReferenceManager.ReferenceType.eAssembly
                        lRefInfo.Name = lParts(1)
                    Case "Package"
                        lRefInfo.Type = ReferenceManager.ReferenceType.ePackage
                        lRefInfo.Name = lParts(1)
                    Case "Project"
                        lRefInfo.Type = ReferenceManager.ReferenceType.eProject
                        lRefInfo.Path = lParts(1)
                        lRefInfo.Name = System.IO.Path.GetFileNameWithoutExtension(lParts(1))
                End Select
                
                Return lRefInfo
                
            Catch ex As Exception
                Console.WriteLine($"error getting Reference info: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ' Event handlers
        Private Sub OnAddAssemblyReference(vSender As Object, vE As EventArgs)
            ShowReferenceManager(0) ' Assembly tab
        End Sub
        
        Private Sub OnAddNuGetPackage(vSender As Object, vE As EventArgs)
            ShowReferenceManager(1) ' NuGet tab
        End Sub
        
        Private Sub OnAddProjectReference(vSender As Object, vE As EventArgs)
            ShowReferenceManager(2) ' project tab
        End Sub
        
        ' FIXED: Added missing OnManageReferences event handler
        Private Sub OnManageReferences(vSender As Object, vE As EventArgs)
            ShowReferenceManager(0) ' Show Reference Manager starting at Assembly tab
        End Sub
        
        Private Sub OnRemoveReference(vSender As Object, vE As EventArgs)
            Try
                Dim lRefInfo As ReferenceManager.ReferenceInfo = GetSelectedReferenceInfo()
                If lRefInfo Is Nothing Then Return
                
                Dim lDialog As New MessageDialog(
                    Me.Toplevel,
                    DialogFlags.Modal,
                    MessageType.Question,
                    ButtonsType.YesNo,
                    $"Are you sure you want to remove the Reference to '{lRefInfo.Name}'?"
                )
                
                If lDialog.Run() = CInt(ResponseType.Yes) Then
                    Dim lReferenceManager As New ReferenceManager()
                    
                    If lReferenceManager.RemoveReference(pProjectFile, lRefInfo.Name, lRefInfo.Type) Then
                        ' Reload references
                        LoadProjectReferences()
                        
                        ' Notify main window
                        RaiseEvent ProjectModified()
                        RaiseEvent ReferencesChanged()
                        
                        ' Show success
                        ShowInfo($"Removed Reference to '{lRefInfo.Name}'")
                    Else
                        ShowError($"Failed to remove Reference to '{lRefInfo.Name}'")
                    End If
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"error removing Reference: {ex.Message}")
                ShowError($"error removing Reference: {ex.Message}")
            End Try
        End Sub
        
        ' Show reference manager dialog
        Private Sub ShowReferenceManager(Optional vInitialTab As Integer = 0)
            Try
                Dim lMainWindow As Window = Me.Toplevel
                If lMainWindow IsNot Nothing AndAlso TypeOf lMainWindow Is MainWindow Then
                    CType(lMainWindow, MainWindow).OnManageReferences(Nothing, Nothing, vInitialTab)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error showing Reference manager: {ex.Message}")
            End Try
        End Sub
        
        ' Show info message
        Private Sub ShowInfo(vMessage As String)
            Dim lDialog As New MessageDialog(
                Me.Toplevel,
                DialogFlags.Modal,
                MessageType.Info,
                ButtonsType.Ok,
                vMessage
            )
            lDialog.Run()
            lDialog.Destroy()
        End Sub
        
        ' Show error message
        Private Sub ShowError(vMessage As String)
            Dim lDialog As New MessageDialog(
                Me.Toplevel,
                DialogFlags.Modal,
                MessageType.Error,
                ButtonsType.Ok,
                vMessage
            )
            lDialog.Run()
            lDialog.Destroy()
        End Sub
        
        ' Refresh references (call this from MainWindow when references change)
        Public Sub RefreshReferences()
            LoadProjectReferences()
        End Sub
        
    End Class

End Namespace
