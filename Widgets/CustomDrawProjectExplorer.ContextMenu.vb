' Widgets/CustomDrawProjectExplorer.ContextMenu.vb - Context menu implementation
' Created: 2025-08-17
Imports Gtk
Imports System
Imports System.IO
Imports SimpleIDE.Models

Namespace Widgets
    
    ''' <summary>
    ''' Partial class containing context menu functionality
    ''' </summary>
    Partial Public Class CustomDrawProjectExplorer
        Inherits Box
        
        ' ===== Context Menu Initialization =====
        
        ''' <summary>
        ''' Initializes the context menu
        ''' </summary>
        Private Sub InitializeContextMenu()
            Try
                pContextMenu = New Menu()
                
                ' Add New Item submenu
                CreateAddNewItemMenu()
                
                pContextMenu.Append(New SeparatorMenuItem())
                
                ' Cut
                Dim lCutItem As New MenuItem("Cut")
                AddHandler lCutItem.Activated, AddressOf OnContextMenuCut
                pContextMenu.Append(lCutItem)
                
                ' Copy
                Dim lCopyItem As New MenuItem("Copy")
                AddHandler lCopyItem.Activated, AddressOf OnContextMenuCopy
                pContextMenu.Append(lCopyItem)
                
                ' Paste
                Dim lPasteItem As New MenuItem("Paste")
                AddHandler lPasteItem.Activated, AddressOf OnContextMenuPaste
                pContextMenu.Append(lPasteItem)
                
                pContextMenu.Append(New SeparatorMenuItem())
                
                ' Delete
                Dim lDeleteItem As New MenuItem("Delete")
                AddHandler lDeleteItem.Activated, AddressOf OnContextMenuDelete
                pContextMenu.Append(lDeleteItem)
                
                ' Rename
                Dim lRenameItem As New MenuItem("Rename...")
                AddHandler lRenameItem.Activated, AddressOf OnContextMenuRename
                pContextMenu.Append(lRenameItem)
                
                pContextMenu.Append(New SeparatorMenuItem())
                
                ' Exclude From Project
                Dim lExcludeItem As New MenuItem("Exclude From Project")
                AddHandler lExcludeItem.Activated, AddressOf OnContextMenuExclude
                pContextMenu.Append(lExcludeItem)
                
                pContextMenu.Append(New SeparatorMenuItem())
                
                ' Open in File Manager
                Dim lOpenFolderItem As New MenuItem("Open in File Manager")
                AddHandler lOpenFolderItem.Activated, AddressOf OnContextMenuOpenInFileManager
                pContextMenu.Append(lOpenFolderItem)
                
                ' Open in Terminal
                Dim lOpenTerminalItem As New MenuItem("Open in Terminal")
                AddHandler lOpenTerminalItem.Activated, AddressOf OnContextMenuOpenInTerminal
                pContextMenu.Append(lOpenTerminalItem)
                
                pContextMenu.Append(New SeparatorMenuItem())
                
                ' Properties
                Dim lPropertiesItem As New MenuItem("Properties")
                AddHandler lPropertiesItem.Activated, AddressOf OnContextMenuProperties
                pContextMenu.Append(lPropertiesItem)
                
                pContextMenu.ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"InitializeContextMenu error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Creates the Add New Item submenu
        ''' </summary>
        Private Sub CreateAddNewItemMenu()
            Try
                Dim lAddMenuItem As New MenuItem("Add")
                Dim lAddSubmenu As New Menu()
                
                ' New Item
                Dim lNewItemItem As New MenuItem("New Item...")
                AddHandler lNewItemItem.Activated, AddressOf OnContextMenuAddNewItem
                lAddSubmenu.Append(lNewItemItem)
                
                ' Existing Item
                Dim lExistingItem As New MenuItem("Existing Item...")
                AddHandler lExistingItem.Activated, AddressOf OnContextMenuAddExistingItem
                lAddSubmenu.Append(lExistingItem)
                
                lAddSubmenu.Append(New SeparatorMenuItem())
                
                ' New Folder
                Dim lNewFolderItem As New MenuItem("New Folder")
                AddHandler lNewFolderItem.Activated, AddressOf OnContextMenuAddNewFolder
                lAddSubmenu.Append(lNewFolderItem)
                
                lAddSubmenu.Append(New SeparatorMenuItem())
                
                ' Class
                Dim lClassItem As New MenuItem("Class...")
                AddHandler lClassItem.Activated, AddressOf OnContextMenuAddClass
                lAddSubmenu.Append(lClassItem)
                
                ' Module
                Dim lModuleItem As New MenuItem("Module...")
                AddHandler lModuleItem.Activated, AddressOf OnContextMenuAddModule
                lAddSubmenu.Append(lModuleItem)
                
                ' Interface
                Dim lInterfaceItem As New MenuItem("Interface...")
                AddHandler lInterfaceItem.Activated, AddressOf OnContextMenuAddInterface
                lAddSubmenu.Append(lInterfaceItem)
                
                ' Form
                Dim lFormItem As New MenuItem("Windows Form...")
                AddHandler lFormItem.Activated, AddressOf OnContextMenuAddForm
                lAddSubmenu.Append(lFormItem)
                
                ' User Control
                Dim lUserControlItem As New MenuItem("User Control...")
                AddHandler lUserControlItem.Activated, AddressOf OnContextMenuAddUserControl
                lAddSubmenu.Append(lUserControlItem)
                
                lAddMenuItem.Submenu = lAddSubmenu
                pContextMenu.Append(lAddMenuItem)
                
            Catch ex As Exception
                Console.WriteLine($"CreateAddNewItemMenu error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Shows the context menu at the current mouse position
        ''' </summary>
        Private Sub ShowContextMenu(vEvent As Gdk.Event)
            Try
                ' Customize menu based on selection
                CustomizeContextMenu()
                
                ' Show the menu
                pContextMenu.ShowAll()
                pContextMenu.PopupAtPointer(vEvent)
                
            Catch ex As Exception
                Console.WriteLine($"ShowContextMenu error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Customizes context menu items based on current selection
        ''' </summary>
        Private Sub CustomizeContextMenu()
            Try
                If pSelectedNode Is Nothing OrElse pSelectedNode.Node Is Nothing Then
                    ' Disable most items if nothing selected
                    SetMenuSensitivity(False)
                    Return
                End If
                
                SetMenuSensitivity(True)
                
                ' Customize based on node type
                Dim lIsFile As Boolean = pSelectedNode.Node.IsFile
                Dim lIsProject As Boolean = pSelectedNode.Node.NodeType = ProjectNodeType.eProject
                Dim lIsSpecial As Boolean = pSelectedNode.Node.NodeType = ProjectNodeType.eReferences OrElse
                                           pSelectedNode.Node.NodeType = ProjectNodeType.eResources OrElse
                                           pSelectedNode.Node.NodeType = ProjectNodeType.eManifest
                
                ' Can't delete or rename project node or special nodes
                If lIsProject OrElse lIsSpecial Then
                    SetMenuItemSensitivity("Delete", False)
                    SetMenuItemSensitivity("Rename...", False)
                    SetMenuItemSensitivity("Exclude From Project", False)
                End If
                
                ' Add items only for folders
                If lIsFile Then
                    SetMenuItemSensitivity("Add", False)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CustomizeContextMenu error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Sets sensitivity for all menu items
        ''' </summary>
        Private Sub SetMenuSensitivity(vSensitive As Boolean)
            Try
                For Each lChild As Widget In pContextMenu.Children
                    If TypeOf lChild Is MenuItem Then
                        lChild.Sensitive = vSensitive
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"SetMenuSensitivity error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Sets sensitivity for a specific menu item
        ''' </summary>
        Private Sub SetMenuItemSensitivity(vLabel As String, vSensitive As Boolean)
            Try
                For Each lChild As Widget In pContextMenu.Children
                    If TypeOf lChild Is MenuItem Then
                        Dim lMenuItem As MenuItem = DirectCast(lChild, MenuItem)
                        If lMenuItem.Label = vLabel Then
                            lMenuItem.Sensitive = vSensitive
                            Exit For
                        End If
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"SetMenuItemSensitivity error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Context Menu Event Handlers =====
        
        Private Sub OnContextMenuCut(vSender As Object, vArgs As EventArgs)
            Try
                ' TODO: Implement cut functionality
                Console.WriteLine("Cut not yet implemented")
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuCut error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnContextMenuCopy(vSender As Object, vArgs As EventArgs)
            Try
                ' TODO: Implement copy functionality
                Console.WriteLine("Copy not yet implemented")
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuCopy error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnContextMenuPaste(vSender As Object, vArgs As EventArgs)
            Try
                ' TODO: Implement paste functionality
                Console.WriteLine("Paste not yet implemented")
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuPaste error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnContextMenuDelete(vSender As Object, vArgs As EventArgs)
            Try
                If pSelectedNode?.Node Is Nothing Then Return
                
                Dim lPath As String = pSelectedNode.Node.Path
                If String.IsNullOrEmpty(lPath) Then Return
                
                ' Confirm deletion
                Dim lDialog As New MessageDialog(
                    Me.Toplevel,
                    DialogFlags.Modal,
                    MessageType.Question,
                    ButtonsType.YesNo,
                    $"Are you sure you want to delete '{pSelectedNode.Node.Name}'?")
                
                If lDialog.Run() = CInt(ResponseType.Yes) Then
                    If pSelectedNode.Node.IsFile Then
                        File.Delete(lPath)
                    Else
                        Directory.Delete(lPath, True)
                    End If
                    
                    ' Refresh project
                    RefreshProject()
                    RaiseEvent ProjectModified()
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuDelete error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnContextMenuRename(vSender As Object, vArgs As EventArgs)
            Try
                If pSelectedNode?.Node Is Nothing Then Return
                If Not pSelectedNode.Node.IsFile Then Return
                
                ' Create rename dialog
                Dim lDialog As New Dialog("Rename File", 
                                        Me.Toplevel, 
                                        DialogFlags.Modal Or DialogFlags.DestroyWithParent,
                                        "Cancel", ResponseType.Cancel,
                                        "Rename", ResponseType.Ok)
                
                lDialog.SetDefaultSize(400, 120)
                
                ' Create entry with current name (FIXED: using System.IO.Path)
                Dim lEntry As New Entry()
                Dim lFileName As String = System.IO.Path.GetFileName(pSelectedNode.Node.Path)
                lEntry.Text = lFileName
                lEntry.SelectRegion(0, lFileName.LastIndexOf("."c))
                
                Dim lLabel As New Label("New name:")
                Dim lBox As New Box(Orientation.Horizontal, 5)
                lBox.PackStart(lLabel, False, False, 5)
                lBox.PackStart(lEntry, True, True, 5)
                
                lDialog.ContentArea.PackStart(lBox, False, False, 10)
                lDialog.ShowAll()
                
                If lDialog.Run() = CInt(ResponseType.Ok) Then
                    Dim lNewName As String = lEntry.Text.Trim()
                    If Not String.IsNullOrEmpty(lNewName) AndAlso lNewName <> lFileName Then
                        ' Rename the file (FIXED: using System.IO.Path)
                        Dim lDirectory As String = System.IO.Path.GetDirectoryName(pSelectedNode.Node.Path)
                        Dim lNewPath As String = System.IO.Path.Combine(lDirectory, lNewName)
                        
                        Try
                            System.IO.File.Move(pSelectedNode.Node.Path, lNewPath)
                            
                            ' Update node
                            pSelectedNode.Node.Name = lNewName
                            pSelectedNode.Node.Path = lNewPath
                            
                            ' Refresh display
                            RebuildVisualTree()
                            pDrawingArea?.QueueDraw()
                            
                            ' Notify of change
                            RaiseEvent ProjectModified()
                            
                        Catch ex As IOException
                            Dim lErrorDialog As New MessageDialog(
                                Me.Toplevel,
                                DialogFlags.Modal,
                                MessageType.Error,
                                ButtonsType.Ok,
                                $"Failed to rename file: {ex.Message}")
                            lErrorDialog.Run()
                            lErrorDialog.Destroy()
                        End Try
                    End If
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuRename error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnContextMenuExclude(vSender As Object, vArgs As EventArgs)
            Try
                ' TODO: Implement exclude from project
                Console.WriteLine("Exclude from project not yet implemented")
                RaiseEvent ProjectModified()
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuExclude error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnContextMenuOpenInFileManager(vSender As Object, vArgs As EventArgs)
            Try
                If pSelectedNode?.Node Is Nothing Then Return
                
                Dim lPath As String = If(pSelectedNode.Node.IsFile,
                                        System.IO.Path.GetDirectoryName(pSelectedNode.Node.Path),
                                        pSelectedNode.Node.Path)
                
                If Directory.Exists(lPath) Then
                    System.Diagnostics.Process.Start("xdg-open", lPath)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuOpenInFileManager error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnContextMenuOpenInTerminal(vSender As Object, vArgs As EventArgs)
            Try
                If pSelectedNode?.Node Is Nothing Then Return
                
                Dim lPath As String = If(pSelectedNode.Node.IsFile,
                                        System.IO.Path.GetDirectoryName(pSelectedNode.Node.Path),
                                        pSelectedNode.Node.Path)
                
                If Directory.Exists(lPath) Then
                    System.Diagnostics.Process.Start("gnome-terminal", $"--working-directory=""{lPath}""")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuOpenInTerminal error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnContextMenuProperties(vSender As Object, vArgs As EventArgs)
            Try
                ' TODO: Implement properties dialog
                Console.WriteLine("Properties not yet implemented")
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuProperties error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Add New Item Handlers =====
        
        Private Sub OnContextMenuAddNewItem(vSender As Object, vArgs As EventArgs)
            Try
                ' TODO: Implement new item dialog
                Console.WriteLine("Add new item not yet implemented")
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuAddNewItem error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnContextMenuAddExistingItem(vSender As Object, vArgs As EventArgs)
            Try
                ' TODO: Implement add existing item dialog
                Console.WriteLine("Add existing item not yet implemented")
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuAddExistingItem error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnContextMenuAddNewFolder(vSender As Object, vArgs As EventArgs)
            Try
                ' TODO: Implement new folder creation
                Console.WriteLine("Add new folder not yet implemented")
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuAddNewFolder error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnContextMenuAddClass(vSender As Object, vArgs As EventArgs)
            Try
                ' TODO: Implement add class dialog
                Console.WriteLine("Add class not yet implemented")
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuAddClass error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnContextMenuAddModule(vSender As Object, vArgs As EventArgs)
            Try
                ' TODO: Implement add module dialog
                Console.WriteLine("Add module not yet implemented")
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuAddModule error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnContextMenuAddInterface(vSender As Object, vArgs As EventArgs)
            Try
                ' TODO: Implement add interface dialog
                Console.WriteLine("Add interface not yet implemented")
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuAddInterface error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnContextMenuAddForm(vSender As Object, vArgs As EventArgs)
            Try
                ' TODO: Implement add form dialog
                Console.WriteLine("Add form not yet implemented")
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuAddForm error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnContextMenuAddUserControl(vSender As Object, vArgs As EventArgs)
            Try
                ' TODO: Implement add user control dialog
                Console.WriteLine("Add user control not yet implemented")
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuAddUserControl error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace