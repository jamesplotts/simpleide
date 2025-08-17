' ProjectExplorer.Manifest.vb - Manifest node integration for Project Explorer
Imports Gtk
Imports System.IO
Imports SimpleIDE.Editors
Imports SimpleIDE.Models
Imports SimpleIDE.Managers

Namespace Widgets
    
    Partial Public Class ProjectExplorer
        
        ' Manifest node constants
        Private Const MANIFEST_NODE_NAME As String = "Assembly Manifest"
        Private pManifestIter As TreeIter
        Private pHasManifestNode As Boolean = False
        
        ' Events
        Public Event ManifestSelected()
        Public Event ManifestChanged()
        
        ' Create manifest node
        Public Sub CreateManifestNode()
            Try
                If pHasManifestNode OrElse pTreeStore Is Nothing Then
                    Return
                End If
                
                ' Get root iter
                Dim lRootIter As TreeIter
                If Not pTreeStore.GetIterFirst(lRootIter) Then
                    Return
                End If
                
                ' Check if manifest already exists as a file node and remove it
                RemoveExistingManifestFileNode(lRootIter)
                
                ' Insert manifest node after project file but before other nodes
                ' Find insertion point (after project file)
                Dim lInsertAfter As TreeIter = lRootIter
                Dim lChildIter As TreeIter
                Dim lFoundProjectFile As Boolean = False
                
                If pTreeStore.IterChildren(lChildIter, lRootIter) Then
                    Do
                        Dim lNodeType As String = CType(pTreeStore.GetValue(lChildIter, 3), String)
                        Dim lIsFile As Boolean = CType(pTreeStore.GetValue(lChildIter, 2), Boolean)
                        Dim lPath As String = CType(pTreeStore.GetValue(lChildIter, 1), String)
                        
                        ' Check if it's the project file
                        If lIsFile AndAlso lPath.EndsWith(".vbproj") Then
                            lInsertAfter = lChildIter
                            lFoundProjectFile = True
                            Exit Do
                        End If
                    Loop While pTreeStore.IterNext(lChildIter)
                End If
                
                ' Insert manifest node after project file
                If lFoundProjectFile Then
                    pManifestIter = pTreeStore.InsertWithValues(lRootIter, 
                        pTreeStore.IterNChildren(lRootIter), 
                        MANIFEST_NODE_NAME,
                        GetManifestPath(),
                        True,
                        "manifest")
                Else
                    ' If no project file found, insert as first child
                    pManifestIter = pTreeStore.InsertWithValues(lRootIter, 
                        0,
                        MANIFEST_NODE_NAME,
                        GetManifestPath(),
                        True,
                        "manifest")            
                End If
                
                ' Set manifest node data
                pTreeStore.SetValues(pManifestIter,
                    MANIFEST_NODE_NAME,                    ' Column 0: String (Name)
                    GetManifestPath(),                     ' Column 1: String (full Path)
                    True,                                  ' Column 2: Boolean (is file)
                    "manifest"                             ' Column 3: String (Node Type)
                )
                
                pHasManifestNode = True
                
                ' Update context menu
                AddManifestContextMenuItems()
                
            Catch ex As Exception
                Console.WriteLine($"error creating manifest Node: {ex.Message}")
            End Try
        End Sub
        
        ' Remove existing manifest file node if it exists
        Private Sub RemoveExistingManifestFileNode(vRootIter As TreeIter)
            Try
                Dim lChildIter As TreeIter
                If pTreeStore.IterChildren(lChildIter, vRootIter) Then
                    Do
                        Dim lPath As String = CType(pTreeStore.GetValue(lChildIter, 1), String)
                        If lPath.EndsWith("app.manifest") Then
                            pTreeStore.Remove(lChildIter)
                            Exit Do
                        End If
                    Loop While pTreeStore.IterNext(lChildIter)
                End If
            Catch ex As Exception
                Console.WriteLine($"error removing existing manifest Node: {ex.Message}")
            End Try
        End Sub
        
        ' Get manifest path
        Private Function GetManifestPath() As String
            Return System.IO.Path.Combine(pProjectDirectory, "app.manifest")
        End Function
        
        ' Check if manifest exists
        Private Function ManifestExists() As Boolean
            Return File.Exists(GetManifestPath())
        End Function
        
        ' Add manifest context menu items
        Private Sub AddManifestContextMenuItems()
            Try
                ' Check if already added
                For Each lItem In pContextMenu.Children
                    If TypeOf lItem Is MenuItem Then
                        Dim lMenuItem As MenuItem = CType(lItem, MenuItem)
                        If lMenuItem.Name = "EditManifest" Then
                            Return ' Already added
                        End If
                    End If
                Next
                
                ' Find position to insert (after Remove Reference)
                Dim lInsertPos As Integer = pContextMenu.Children.Length
                For i As Integer = 0 To pContextMenu.Children.Length - 1
                    Dim lItem As Widget = pContextMenu.Children(i)
                    If TypeOf lItem Is MenuItem Then
                        Dim lMenuItem As MenuItem = CType(lItem, MenuItem)
                        If lMenuItem.Name = "RemoveReference" Then
                            lInsertPos = i + 1
                            Exit For
                        End If
                    End If
                Next
                
                ' Add separator
                Dim lSeparator As New SeparatorMenuItem()
                lSeparator.Name = "ManifestSeparator"
                lSeparator.NoShowAll = True
                pContextMenu.Insert(lSeparator, lInsertPos)
                
                ' Edit Manifest
                Dim lEditManifestItem As New MenuItem("Edit Manifest")
                lEditManifestItem.Name = "EditManifest"
                lEditManifestItem.NoShowAll = True
                AddHandler lEditManifestItem.Activated, AddressOf OnEditManifest
                pContextMenu.Insert(lEditManifestItem, lInsertPos + 1)
                
                ' Edit Assembly Settings
                Dim lEditAssemblyItem As New MenuItem("Assembly Settings...")
                lEditAssemblyItem.Name = "EditAssemblySettings"
                lEditAssemblyItem.NoShowAll = True
                AddHandler lEditAssemblyItem.Activated, AddressOf OnEditAssemblySettings
                pContextMenu.Insert(lEditAssemblyItem, lInsertPos + 2)
                
                pContextMenu.ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"error adding manifest Context menu items: {ex.Message}")
            End Try
        End Sub
        
        ' Update context menu for manifest
        Private Sub UpdateManifestContextMenu()
            Try
                ' Get menu items
                Dim lManifestSeparator As Widget = Nothing
                Dim lEditManifest As Widget = Nothing
                Dim lEditAssemblySettings As Widget = Nothing
                
                For Each lChild In pContextMenu.Children
                    If lChild.Name = "ManifestSeparator" Then lManifestSeparator = lChild
                    If lChild.Name = "EditManifest" Then lEditManifest = lChild
                    If lChild.Name = "EditAssemblySettings" Then lEditAssemblySettings = lChild
                Next
                
                ' Check if current selection is manifest node
                Dim lIsManifestNode As Boolean = IsManifestNode()
                
                ' Show/hide manifest items
                If lManifestSeparator IsNot Nothing Then
                    lManifestSeparator.Visible = lIsManifestNode
                End If
                
                If lEditManifest IsNot Nothing Then
                    lEditManifest.Visible = lIsManifestNode
                End If
                
                If lEditAssemblySettings IsNot Nothing Then
                    lEditAssemblySettings.Visible = lIsManifestNode
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error updating manifest Context menu: {ex.Message}")
            End Try
        End Sub
        
        ' Check if current selection is manifest node
        Private Function IsManifestNode() As Boolean
            Try
                If Not pCurrentIsFile Then Return False
                
                ' Check if it's the manifest node by type
                Dim lNodeType As String = ""
                If pCurrentSelectedIter.UserData <> IntPtr.Zero Then
                    lNodeType = CType(pTreeStore.GetValue(pCurrentSelectedIter, 3), String)
                End If
                
                Return lNodeType = "manifest" OrElse pCurrentSelectedPath.EndsWith("app.manifest")
                
            Catch ex As Exception
                Console.WriteLine($"error checking manifest Node: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Handle Edit Manifest menu item  
        Private Sub OnEditManifest(vSender As Object, vE As EventArgs)
            Try
                ' Show Assembly Settings dialog by calling the main window method
                Dim lMainWindow As Window = Me.Toplevel
                If lMainWindow IsNot Nothing AndAlso TypeOf lMainWindow Is MainWindow Then
                    CType(lMainWindow, MainWindow).ShowAssemblySettings()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error showing assembly settings: {ex.Message}")
            End Try
        End Sub
        
        ' Handle Edit Assembly Settings menu item
        Private Sub OnEditAssemblySettings(vSender As Object, vE As EventArgs)
            Try
                ' Get main window and show assembly settings
                Dim lMainWindow As Window = Me.Toplevel
                If lMainWindow IsNot Nothing AndAlso TypeOf lMainWindow Is MainWindow Then
                    CType(lMainWindow, MainWindow).ShowAssemblySettings()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error showing assembly settings: {ex.Message}")
            End Try
        End Sub
        
        ' Update manifest node status
        Public Sub UpdateManifestNode()
            Try
                If Not pHasManifestNode Then Return
                
                ' Check if manifest file exists
                Dim lExists As Boolean = ManifestExists()
                Dim lVersionManager As New AssemblyVersionManager(pProjectFile)
                Dim lIsEmbedded As Boolean = lVersionManager.IsManifestEmbeddingEnabled()
                
                ' Update display name
                Dim lDisplayName As String = MANIFEST_NODE_NAME
                If lIsEmbedded Then
                    lDisplayName &= " [Embedded]"
                ElseIf Not lExists Then
                    lDisplayName &= " [Not Found]"
                End If
                
                ' Update node
                pTreeStore.SetValue(pManifestIter, 0, lDisplayName)
                
            Catch ex As Exception
                Console.WriteLine($"error updating manifest Node: {ex.Message}")
            End Try
        End Sub
        
        ' Refresh manifest node (call after manifest changes)
        Public Sub RefreshManifestNode()
            UpdateManifestNode()
        End Sub
        
        ' Handle manifest node in row activation
        Private Function HandleManifestActivation(vPath As String) As Boolean
            Try
                If IsManifestNode() Then
                    RaiseEvent ManifestSelected()
                    Return True
                End If
                Return False
            Catch ex As Exception
                Console.WriteLine($"error handling manifest activation: {ex.Message}")
                Return False
            End Try
        End Function
        
    End Class

End Namespace