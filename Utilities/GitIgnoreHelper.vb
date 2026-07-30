' Utilities/GitIgnoreHelper.vb - Complete helper for creating .gitignore files
Imports System.IO
Imports System.Text

Namespace Utilities
    
    Public Class GitIgnoreHelper
        
        ' Default .gitignore content for VB.NET projects
        Private Shared ReadOnly DefaultGitIgnore As String = "# Visual Studio / .NET
## User-specific files
*.rsuser
*.suo
*.user
*.userosscache
*.sln.docstates

## Build results
[Dd]eBug/
[Dd]ebugPublic/
[Rr]elease/
[Rr]eleases/
x64/
x86/
[Aa][Rr][Mm]/
[Aa][Rr][Mm]64/
bld/
[Bb]In/
[Oo]bj/
[Ll]og/
[Ll]ogs/

## Visual Studio cache/options
.vs/
.vscode/

## .NET Core
project.lock.json
project.fragment.lock.json
Artifacts/

## NuGet
*.nupkg
*.snupkg
# the Packages folder can be ignored because Of Package Restore
**/[Pp]ackages/*
# except build/, which Is used As an MSBuild target.
!**/[Pp]ackages/build/
# Uncomment If Using NuGet v1 And the Package restore approach
# !**/Packages/repositories.config
# !**/Packages.config

## Microsoft Azure Web App publish settings
PublishProfiles/

## Microsoft Azure App Service publish settings
*.PublishSettings
**/PublishProfiles/**
*.azurePubxml

## Microsoft Azure Web App publish settings  
*.pubxml
*.publishproj

# Microsoft Azure Build output
csx/
*.build.csdef

# Microsoft Azure Emulator
ecf/
rcf/

# Windows Store app Package directories And files
AppPackages/
BundleArtifacts/
Package.StoreAssociation.xml
_pkginfo.txt
*.appx

# Visual Studio cache files
# files ending In .cache can be ignored
*.[Cc]ache
# but keep track Of directories ending In .cache
!?*.[Cc]ache/

# Others
ClientBin/
~$*
*~
*.dbmdl
*.dbproj.schemaview
*.jfm
*.pfx
*.publishsettings
orleans.codegen.cs

# Including strong Name files can present a security risk
# (https://github.com/github/gitignore/Pull/2483#issue-259490424)
#*.snk

# Since there are multiple workflows, uncomment Next Line To ignore bower_components
# (https://github.com/github/gitignore/Pull/1529#issuecomment-104372622)
#bower_components/

# RIA/Silverlight projects
Generated_Code/

# Backup & report files from converting an old project file
# To a newer Visual Studio Version. Backup files are Not needed,
# because we have git ;-)
_UpgradeReport_Files/
Backup*/
UpgradeLog*.XML
UpgradeLog*.htm
ServiceFabricBackup/
*.rptproj.bak

# SQL Server files
*.mdf
*.ldf
*.ndf

# Business Intelligence projects
*.rdl.Data
*.bim.layout
*.bim_*.settings
*.rptproj.rsuser
*- Backup*.rdl

# Microsoft Fakes
FakesAssemblies/

# GhostDoc plugin setting file
*.GhostDoc.xml

# Node.js Tools For Visual Studio
.ntvs_analysis.dat
node_modules/

# Visual Studio 6 build log
*.plg

# Visual Studio 6 workspace options file
*.opt

# Visual Studio 6 auto-generated workspace file (contains which files were open etc.)
*.vbw

# Visual Studio LightSwitch build output
**/*.HTMLClient/GeneratedArtifacts
**/*.DesktopClient/GeneratedArtifacts
**/*.DesktopClient/ModelManifest.xml
**/*.Server/GeneratedArtifacts
**/*.Server/ModelManifest.xml
_Pvt_Extensions

# Paket dependency manager
.paket/paket.exe
paket-files/

# FAKE - F# Make
.fake/

# JetBrains Rider
.idea/
*.sln.iml

# CodeRush personal settings
.cr/personal

# Python Tools For Visual Studio (PTVS)
__pycache__/
*.pyc

# Cake - Uncomment If you are Using it
# tools/**
# !tools/Packages.config

# Tabs Studio
*.tss

# Telerik's JustMock Configuration file
*.jmconfig

# BizTalk build output
*.btp.cs
*.btm.cs
*.odx.cs
*.xsd.cs

# OpenCover UI analysis results
OpenCover/

# Azure Stream Analytics local run output
ASALocalRun/

# MSBuild Binary And Structured Log
*.binlog

# NVidia Nsight GPU debugger Configuration file
*.nvuser

# MFractors (Xamarin productivity tool) working folder
.mfractor/

# Local History For Visual Studio
.localhistory/

# BeatPulse healthcheck temp database
healthchecksdb

# Backup folder For Package Reference Convert tool In Visual Studio 2017
MigrationBackup/

# SimpleIDE specific
SimpleIDE.log
*.tmp
.simpleide/
SimpleIDE.config
"

        ' Create .gitignore file in the specified directory
        Public Shared Function CreateGitIgnore(vProjectPath As String) As Boolean
            Try
                If String.IsNullOrEmpty(vProjectPath) OrElse Not Directory.Exists(vProjectPath) Then
                    Console.WriteLine("Invalid project Path for .gitignore creation")
                    Return False
                End If
                
                Dim lGitIgnorePath As String = System.IO.Path.Combine(vProjectPath, ".gitignore")
                
                ' Check if .gitignore already exists
                If File.Exists(lGitIgnorePath) Then
                    Console.WriteLine(".gitignore already exists")
                    Return True
                End If
                
                ' Write default content
                File.WriteAllText(lGitIgnorePath, DefaultGitIgnore, Encoding.UTF8)
                Console.WriteLine($"Created .gitignore at: {lGitIgnorePath}")
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"error creating .gitignore: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Add custom patterns to existing .gitignore
        Public Shared Function AddToGitIgnore(vProjectPath As String, vPatterns() As String) As Boolean
            Try
                If String.IsNullOrEmpty(vProjectPath) OrElse Not Directory.Exists(vProjectPath) Then
                    Return False
                End If
                
                Dim lGitIgnorePath As String = System.IO.Path.Combine(vProjectPath, ".gitignore")
                
                ' Create if doesn't exist
                If Not File.Exists(lGitIgnorePath) Then
                    CreateGitIgnore(vProjectPath)
                End If
                
                ' Read existing content
                Dim lExistingContent As String = File.ReadAllText(lGitIgnorePath, Encoding.UTF8)
                
                ' Add new patterns
                Dim lNewContent As New StringBuilder(lExistingContent)
                If Not lExistingContent.EndsWith(Environment.NewLine) Then
                    lNewContent.AppendLine()
                End If
                
                lNewContent.AppendLine("# Custom patterns")
                For Each lPattern In vPatterns
                    If Not String.IsNullOrWhiteSpace(lPattern) AndAlso Not lExistingContent.Contains(lPattern.Trim()) Then
                        lNewContent.AppendLine(lPattern.Trim())
                    End If
                Next
                
                ' Write back
                File.WriteAllText(lGitIgnorePath, lNewContent.ToString(), Encoding.UTF8)
                Console.WriteLine($"Added {vPatterns.Length} patterns to .gitignore")
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"error adding to .gitignore: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Get VB.NET specific patterns
        Public Shared Function GetVBNetPatterns() As String()
            Return {
                "# VB.NET specific",
                "*.vbproj.user",
                "*.vb.user",
                "*.vbproj.vspscc",
                "*.vb.vspscc",
                "*.vbproj.vssscc",
                "*.vb.vssscc"
            }
        End Function
        
        ' Get common development patterns
        Public Shared Function GetDevelopmentPatterns() As String()
            Return {
                "# Development files",
                "*.log",
                "*.tmp",
                "*.temp",
                "*.bak",
                "*.swp",
                "*.swo",
                "*~",
                ".DS_Store",
                "Thumbs.db",
                "desktop.ini"
            }
        End Function
        
        ' Check if .gitignore exists
        Public Shared Function GitIgnoreExists(vProjectPath As String) As Boolean
            Try
                If String.IsNullOrEmpty(vProjectPath) Then Return False
                Dim lGitIgnorePath As String = System.IO.Path.Combine(vProjectPath, ".gitignore")
                Return File.Exists(lGitIgnorePath)
            Catch
                Return False
            End Try
        End Function
        
        ' Read .gitignore content
        Public Shared Function ReadGitIgnore(vProjectPath As String) As String
            Try
                If String.IsNullOrEmpty(vProjectPath) Then Return ""
                Dim lGitIgnorePath As String = System.IO.Path.Combine(vProjectPath, ".gitignore")
                If File.Exists(lGitIgnorePath) Then
                    Return File.ReadAllText(lGitIgnorePath, Encoding.UTF8)
                End If
                Return ""
            Catch ex As Exception
                Console.WriteLine($"error reading .gitignore: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ' Write .gitignore content
        Public Shared Function WriteGitIgnore(vProjectPath As String, vContent As String) As Boolean
            Try
                If String.IsNullOrEmpty(vProjectPath) OrElse Not Directory.Exists(vProjectPath) Then
                    Return False
                End If
                
                Dim lGitIgnorePath As String = System.IO.Path.Combine(vProjectPath, ".gitignore")
                File.WriteAllText(lGitIgnorePath, vContent, Encoding.UTF8)
                Console.WriteLine($"updated .gitignore at: {lGitIgnorePath}")
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"error writing .gitignore: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Get the default .gitignore content
        Public Shared Function GetDefaultContent() As String
            Return DefaultGitIgnore
        End Function
        
        ' Validate .gitignore patterns
        Public Shared Function ValidatePatterns(vPatterns() As String) As List(Of String)
            Dim lValidPatterns As New List(Of String)
            
            Try
                For Each lPattern In vPatterns
                    If Not String.IsNullOrWhiteSpace(lPattern) Then
                        Dim lTrimmed As String = lPattern.Trim()
                        
                        ' Skip comments
                        If lTrimmed.StartsWith("#") Then
                            lValidPatterns.Add(lTrimmed)
                            Continue For
                        End If
                        
                        ' Basic validation - no invalid characters for file patterns
                        If Not lTrimmed.Contains("<") AndAlso Not lTrimmed.Contains(">") AndAlso
                           Not lTrimmed.Contains("|") AndAlso Not lTrimmed.Contains("""") Then
                            lValidPatterns.Add(lTrimmed)
                        End If
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"error validating patterns: {ex.Message}")
            End Try
            
            Return lValidPatterns
        End Function
        
    End Class
    
End Namespace
