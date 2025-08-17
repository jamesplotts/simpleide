' Utilities/HelpSystem.vb - Integrated online help system for SimpleIDE
Imports Gtk
Imports System.Collections.Generic
Imports System.Diagnostics

Namespace Utilities
    Public Class HelpSystem
        
        ' Help resource definitions
        Private Shared ReadOnly pHelpResources As New Dictionary(Of String, HelpResource) From {
            {"gtk-overview", New HelpResource("GTK# Overview", "https://www.mono-project.com/docs/GUI/gtksharp/beginners-guide/", "General GTK# concepts and getting started")},
            {"gtk-api", New HelpResource("GTK 3 API Reference", "https://docs.gtk.org/gtk3/", "Complete GTK 3 API documentation")},
            {"gtk-widgets", New HelpResource("GTK Widget Gallery", "https://docs.gtk.org/gtk3/visual_index.html", "Visual index of GTK widgets")},
            {"gtk-devdocs", New HelpResource("GTK DevDocs", "https://devdocs.io/gtk~3.20/", "Offline-capable GTK documentation")},
            {"dotnet-overview", New HelpResource(".NET 8 documentation", "https://learn.microsoft.com/en-us/dotnet/", "Main .NET documentation hub")},
            {"dotnet-api", New HelpResource(".NET API Browser", "https://learn.microsoft.com/en-us/dotnet/api/", "Comprehensive .NET API Reference")},
            {"dotnet-linux", New HelpResource(".NET on Linux", "https://learn.microsoft.com/en-us/dotnet/core/install/linux", "Linux installation and deployment guide")},
            {"vb-Reference", New HelpResource("VB.NET Reference", "https://learn.microsoft.com/en-us/dotnet/Visual-basic/", "Visual Basic .NET Language Reference")},
            {"aspnet-core", New HelpResource("ASP.NET Core", "https://learn.microsoft.com/en-us/aspnet/core/", "Web API and ASP.NET Core documentation")},
            {"dotnet-cli", New HelpResource(".NET CLI", "https://learn.microsoft.com/en-us/dotnet/core/tools/", "Command-Line interface Reference")}
        }
        
        ' Context-sensitive help mappings
        Private Shared ReadOnly pContextHelp As New Dictionary(Of String, String()) From {
            {"MainWindow", {"gtk-overview", "gtk-widgets"}},
            {"TextBuffer", {"gtk-api", "gtk-devdocs"}},
            {"TreeView", {"gtk-widgets", "gtk-api"}},
            {"BuildManager", {"dotnet-cli", "dotnet-linux"}},
            {"VBSyntaxHighlighter", {"vb-Reference", "dotnet-api"}},
            {"ProjectExplorer", {"dotnet-overview", "vb-Reference"}},
            {"SettingsWindow", {"gtk-overview", "gtk-widgets"}}
        }
        
        ' Help resource data structure
        Public Class HelpResource
            Public Property key As String
            Public Property Title As String
            Public Property Url As String
            Public Property Description As String
            
            Public Sub New(vTitle As String, vUrl As String, vDescription As String)
                Title = vTitle
                Url = vUrl
                Description = vDescription
            End Sub
        End Class
        
        ' Get GTK topics for menu
        Public Shared Function GetGtkTopics() As Dictionary(Of String, HelpResource)
            Dim lResult As New Dictionary(Of String, HelpResource)
            
            For Each lKvp In pHelpResources
                If lKvp.key.StartsWith("gtk-") Then
                    Dim lResource As HelpResource = lKvp.Value
                    lResource.key = lKvp.key ' Set the key property
                    lResult.Add(lKvp.key, lResource)
                End If
            Next
            
            Return lResult
        End Function
        
        ' Get .NET topics for menu
        Public Shared Function GetDotNetTopics() As Dictionary(Of String, HelpResource)
            Dim lResult As New Dictionary(Of String, HelpResource)
            
            For Each lKvp In pHelpResources
                If lKvp.key.StartsWith("dotnet-") OrElse lKvp.key = "vb-Reference" OrElse lKvp.key = "aspnet-core" Then
                    Dim lResource As HelpResource = lKvp.Value
                    lResource.key = lKvp.key ' Set the key property
                    lResult.Add(lKvp.key, lResource)
                End If
            Next
            
            Return lResult
        End Function
        
        ' Show help for a specific topic
        Public Shared Sub ShowHelp(vResourceKey As String)
            Try
                Dim lUrl As String = GetHelpUrl(vResourceKey)
                If Not String.IsNullOrEmpty(lUrl) Then
                    OpenUrl(lUrl)
                End If
            Catch ex As Exception
                Console.WriteLine($"error showing help: {ex.Message}")
            End Try
        End Sub
        
        ' Show help dialog window
        Public Shared Sub ShowHelpWindow(vParent As Window)
            Try
                Dim lDialog As New Dialog("SimpleIDE Help Resources", vParent, DialogFlags.Modal)
                lDialog.SetDefaultSize(800, 600)
                
                ' Set icon
                Try
                    Using lStream As System.IO.Stream = GetType(MainWindow).Assembly.GetManifestResourceStream("SimpleIDE.icon.png")
                        If lStream IsNot Nothing Then
                            lDialog.Icon = New Gdk.Pixbuf(lStream)
                        End If
                    End Using
                Catch ex As Exception
                    Console.WriteLine($"error loading dialog Icon: {ex.Message}")
                End Try
                
                ' Create main box
                Dim lMainBox As New Box(Orientation.Vertical, 12)
                lMainBox.BorderWidth = 12
                
                ' Add sections
                AddHelpSection(lMainBox, "GTK# documentation", GetGtkTopics())
                AddHelpSection(lMainBox, ".NET documentation", GetDotNetTopics())
                
                ' Add to scrolled window
                Dim lScrolled As New ScrolledWindow()
                lScrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
                lScrolled.Add(lMainBox)
                
                lDialog.ContentArea.Add(lScrolled)
                
                ' Add close button
                lDialog.AddButton("Close", ResponseType.Close)
                
                lDialog.ShowAll()
                lDialog.Run()
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"error showing help window: {ex.Message}")
            End Try
        End Sub
        
        ' Helper to add a help section
        Private Shared Sub AddHelpSection(vBox As Box, vTitle As String, vResources As Dictionary(Of String, HelpResource))
            ' Section header
            Dim lHeaderLabel As New Label()
            lHeaderLabel.Markup = $"<b>{vTitle}</b>"
            lHeaderLabel.Halign = Align.Start
            vBox.PackStart(lHeaderLabel, False, False, 0)
            
            ' Resources
            For Each lKvp In vResources
                Dim lResource As HelpResource = lKvp.Value
                
                Dim lLinkButton As New LinkButton(lResource.Url, lResource.Title)
                lLinkButton.Halign = Align.Start
                AddHandler lLinkButton.Clicked, Sub(sender, e) OpenUrl(lResource.Url)
                
                vBox.PackStart(lLinkButton, False, False, 0)
                
                Dim lDescriptionLabel As New Label(lResource.Description)
                lDescriptionLabel.Halign = Align.Start
                lDescriptionLabel.MarginStart = 20
                lDescriptionLabel.LineWrap = True
                vBox.PackStart(lDescriptionLabel, False, False, 0)
            Next
            
            ' Add spacing
            vBox.PackStart(New Label(), False, False, 12)
        End Sub
        
        ' Show context-sensitive help
        Public Shared Sub ShowContextHelp(vParent As Window, vContext As String)
            Try
                If Not pContextHelp.ContainsKey(vContext) Then
                    ShowHelpWindow(vParent)
                    Return
                End If
                
                Dim lContextResources() As String = pContextHelp(vContext)
                Dim lDialog As New Dialog($"Help for {vContext}", vParent, DialogFlags.Modal)
                lDialog.SetDefaultSize(600, 400)
                
                ' Set icon
                Try
                    Using lStream As System.IO.Stream = GetType(MainWindow).Assembly.GetManifestResourceStream("SimpleIDE.icon.png")
                        If lStream IsNot Nothing Then
                            lDialog.Icon = New Gdk.Pixbuf(lStream)
                        End If
                    End Using
                Catch ex As Exception
                    Console.WriteLine($"error loading dialog Icon: {ex.Message}")
                End Try
                
                ' Create content
                Dim lMainBox As New Box(Orientation.Vertical, 12)
                lMainBox.BorderWidth = 12
                
                ' Description
                Dim lDescLabel As New Label($"Help resources for {vContext}:")
                lDescLabel.Halign = Align.Start
                lMainBox.PackStart(lDescLabel, False, False, 0)
                
                ' List resources
                For Each lResourceKey In lContextResources
                    If pHelpResources.ContainsKey(lResourceKey) Then
                        Dim lResource As HelpResource = pHelpResources(lResourceKey)
                        
                        Dim lLinkButton As New LinkButton(lResource.Url, lResource.Title)
                        lLinkButton.Halign = Align.Start
                        AddHandler lLinkButton.Clicked, Sub(sender, e) OpenUrl(lResource.Url)
                        
                        lMainBox.PackStart(lLinkButton, False, False, 0)
                        
                        Dim lDescriptionLabel As New Label(lResource.Description)
                        lDescriptionLabel.Halign = Align.Start
                        lDescriptionLabel.MarginStart = 20
                        lDescriptionLabel.LineWrap = True
                        lMainBox.PackStart(lDescriptionLabel, False, False, 0)
                    End If
                Next
                
                lDialog.ContentArea.Add(lMainBox)
                
                ' Add close button
                lDialog.AddButton("Close", ResponseType.Close)
                
                lDialog.ShowAll()
                lDialog.Run()
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"error showing Context help: {ex.Message}")
            End Try
        End Sub
        
        ' Get help URL for a specific resource
        Public Shared Function GetHelpUrl(vResourceKey As String) As String
            Try
                If pHelpResources.ContainsKey(vResourceKey) Then
                    Return pHelpResources(vResourceKey).Url
                End If
            Catch ex As Exception
                Console.WriteLine($"error getting help Url: {ex.Message}")
            End Try
            Return ""
        End Function
        
        ' Open URL in default browser
        Private Shared Sub OpenUrl(vUrl As String)
            Try
                Dim lProcess As New Process()
                lProcess.StartInfo.FileName = "xdg-open"
                lProcess.StartInfo.Arguments = vUrl
                lProcess.StartInfo.UseShellExecute = False
                lProcess.Start()
            Catch ex As Exception
                Console.WriteLine($"error opening Url: {ex.Message}")
                
                ' Try alternative method
                Try
                    Process.Start(vUrl)
                Catch ex2 As Exception
                    Console.WriteLine($"Alternative method also failed: {ex2.Message}")
                End Try
            End Try
        End Sub
    End Class
End Namespace
