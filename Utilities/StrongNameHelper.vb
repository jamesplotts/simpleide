' Utilities/StrongNameHelper.vb - Strong name key generation and management
Imports System.IO
Imports System.Diagnostics
Imports System.Security.Cryptography

Namespace Utilities
    Public Class StrongNameHelper
        
        ' Generate a strong name key file (.snk)
        Public Shared Function GenerateKeyFile(vPath As String) As Boolean
            Try
                ' First try using sn.exe
                If TryGenerateWithSn(vPath) Then
                    Return True
                End If
                
                ' Try using dotnet sn
                If TryGenerateWithDotnetSn(vPath) Then
                    Return True
                End If
                
                ' Fallback to manual generation
                Return GenerateKeyManually(vPath)
                
            Catch ex As Exception
                Console.WriteLine($"error generating strong Name key: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Try to generate using sn.exe
        Private Shared Function TryGenerateWithSn(vPath As String) As Boolean
            Try
                Dim lSnPath As String = FindSnExecutable()
                If String.IsNullOrEmpty(lSnPath) Then
                    Return False
                End If
                
                Using lProcess As New Process()
                    lProcess.StartInfo.FileName = lSnPath
                    lProcess.StartInfo.Arguments = $"-k ""{vPath}"""
                    lProcess.StartInfo.UseShellExecute = False
                    lProcess.StartInfo.RedirectStandardOutput = True
                    lProcess.StartInfo.RedirectStandardError = True
                    lProcess.StartInfo.CreateNoWindow = True
                    
                    lProcess.Start()
                    lProcess.WaitForExit(10000) ' 10 second timeout
                    
                    If lProcess.ExitCode = 0 Then
                        Console.WriteLine($"Successfully generated key using sn.exe")
                        Return True
                    End If
                End Using
                
            Catch ex As Exception
                Console.WriteLine($"error using sn.exe: {ex.Message}")
            End Try
            
            Return False
        End Function
        
        ' Try to generate using dotnet sn
        Private Shared Function TryGenerateWithDotnetSn(vPath As String) As Boolean
            Try
                Using lProcess As New Process()
                    lProcess.StartInfo.FileName = "dotnet"
                    lProcess.StartInfo.Arguments = $"sn -k ""{vPath}"""
                    lProcess.StartInfo.UseShellExecute = False
                    lProcess.StartInfo.RedirectStandardOutput = True
                    lProcess.StartInfo.RedirectStandardError = True
                    lProcess.StartInfo.CreateNoWindow = True
                    
                    lProcess.Start()
                    lProcess.WaitForExit(10000) ' 10 second timeout
                    
                    If lProcess.ExitCode = 0 Then
                        Console.WriteLine($"Successfully generated key using dotnet sn")
                        Return True
                    End If
                End Using
                
            Catch ex As Exception
                Console.WriteLine($"error using dotnet sn: {ex.Message}")
            End Try
            
            Return False
        End Function
        
        ' Generate key manually using RSACryptoServiceProvider
        Private Shared Function GenerateKeyManually(vPath As String) As Boolean
            Try
                ' Create a new RSA key pair with 2048 bit key
                Using lRsa As New RSACryptoServiceProvider(2048)
                    ' Export the key pair
                    Dim lKeyData() As Byte = lRsa.ExportCspBlob(True)
                    
                    ' Write to file
                    File.WriteAllBytes(vPath, lKeyData)
                    
                    Console.WriteLine($"Successfully generated key manually")
                    Return True
                End Using
                
            Catch ex As Exception
                Console.WriteLine($"error generating key manually: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Find sn.exe executable
        Private Shared Function FindSnExecutable() As String
            Try
                ' Common locations for sn.exe
                Dim lPossiblePaths As New List(Of String)
                
                ' Add Windows SDK paths
                Dim lProgramFiles As String = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
                Dim lProgramFilesX86 As String = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                
                ' Windows SDK locations
                lPossiblePaths.Add(Path.Combine(lProgramFiles, "Microsoft SDKs", "Windows", "v10.0A", "bin", "NETFX 4.8 Tools", "sn.exe"))
                lPossiblePaths.Add(Path.Combine(lProgramFilesX86, "Microsoft SDKs", "Windows", "v10.0A", "bin", "NETFX 4.8 Tools", "sn.exe"))
                lPossiblePaths.Add(Path.Combine(lProgramFiles, "Microsoft SDKs", "Windows", "v8.1A", "bin", "NETFX 4.5.1 Tools", "sn.exe"))
                lPossiblePaths.Add(Path.Combine(lProgramFilesX86, "Microsoft SDKs", "Windows", "v8.1A", "bin", "NETFX 4.5.1 Tools", "sn.exe"))
                
                ' .NET Framework paths
                Dim lFrameworkPath As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET", "Framework64")
                If Directory.Exists(lFrameworkPath) Then
                    For Each lVersionDir In Directory.GetDirectories(lFrameworkPath, "v*")
                        lPossiblePaths.Add(Path.Combine(lVersionDir, "sn.exe"))
                    Next
                End If
                
                ' Check each path
                For Each lPath In lPossiblePaths
                    If File.Exists(lPath) Then
                        Console.WriteLine($"Found sn.exe at: {lPath}")
                        Return lPath
                    End If
                Next
                
                ' Try using 'where' command on Windows
                If Environment.OSVersion.Platform = PlatformID.Win32NT Then
                    Using lProcess As New Process()
                        lProcess.StartInfo.FileName = "where"
                        lProcess.StartInfo.Arguments = "sn.exe"
                        lProcess.StartInfo.UseShellExecute = False
                        lProcess.StartInfo.RedirectStandardOutput = True
                        lProcess.StartInfo.CreateNoWindow = True
                        
                        lProcess.Start()
                        Dim lOutput As String = lProcess.StandardOutput.ReadToEnd().Trim()
                        lProcess.WaitForExit()
                        
                        If lProcess.ExitCode = 0 AndAlso Not String.IsNullOrEmpty(lOutput) Then
                            ' Take first result if multiple found
                            Dim lLines() As String = lOutput.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
                            If lLines.Length > 0 Then
                                Console.WriteLine($"Found sn.exe via where: {lLines(0)}")
                                Return lLines(0)
                            End If
                        End If
                    End Using
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error finding sn.exe: {ex.Message}")
            End Try
            
            Return String.Empty
        End Function
        
        ' Extract public key from a strong name key file
        Public Shared Function ExtractPublicKey(vKeyFile As String) As Byte()
            Try
                If Not File.Exists(vKeyFile) Then
                    Throw New FileNotFoundException("key file not found", vKeyFile)
                End If
                
                ' Read the key file
                Dim lKeyData() As Byte = File.ReadAllBytes(vKeyFile)
                
                ' Try to load as RSA key
                Using lRsa As New RSACryptoServiceProvider()
                    Try
                        lRsa.ImportCspBlob(lKeyData)
                        ' Export public key only
                        Return lRsa.ExportCspBlob(False)
                    Catch
                        ' If it's already a public key, return as is
                        Return lKeyData
                    End Try
                End Using
                
            Catch ex As Exception
                Console.WriteLine($"error extracting public key: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ' Verify if a file is a valid strong name key
        Public Shared Function IsValidKeyFile(vPath As String) As Boolean
            Try
                If Not File.Exists(vPath) Then
                    Return False
                End If
                
                ' Try to load the key
                Dim lKeyData() As Byte = File.ReadAllBytes(vPath)
                
                ' Check file size (valid keys are typically between 160-600 bytes)
                If lKeyData.Length < 160 OrElse lKeyData.Length > 1024 Then
                    Return False
                End If
                
                ' Try to import as RSA key
                Using lRsa As New RSACryptoServiceProvider()
                    Try
                        lRsa.ImportCspBlob(lKeyData)
                        Return True
                    Catch
                        Return False
                    End Try
                End Using
                
            Catch ex As Exception
                Console.WriteLine($"error validating key file: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Generate a password-protected certificate (.pfx)
        Public Shared Function GeneratePfxFile(vPath As String, vPassword As String, vCertificateName As String) As Boolean
            Try
                ' This would require X509 certificate creation
                ' For now, return false as this requires additional implementation
                Console.WriteLine("PFX generation requires X509 certificate tools")
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"error generating PFX: {ex.Message}")
                Return False
            End Try
        End Function
    End Class
End Namespace
