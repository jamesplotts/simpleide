' Utilities/CredentialManager.vb - Secure credential storage manager for Linux
Imports System
Imports System.IO
Imports System.Diagnostics
Imports System.Security.Cryptography
Imports System.Text

' CredentialManager.vb
' Created: 2025-08-20 22:53:42

Namespace Utilities
    
    ''' <summary>
    ''' Manages secure credential storage on Linux systems
    ''' </summary>
    Public Class CredentialManager
        
        ' ===== Enumerations =====
        
        ''' <summary>
        ''' Available credential storage methods
        ''' </summary>
        Public Enum eStorageMethod
            eUnspecified
            eGnomeKeyring      ' Uses gnome-keyring via secret-tool
            eLibSecret         ' Uses libsecret directly
            eKWallet           ' KDE Wallet (for KDE users)
            eEncryptedFile     ' Local encrypted file (fallback)
            eLastValue
        End Enum
        
        ' ===== Private Fields =====
        Private pStorageMethod As eStorageMethod
        Private pApplicationName As String = "SimpleIDE"
        Private pEncryptionKey As Byte()
        Private pCredentialFilePath As String
        
        ' ===== Constructor =====
        
        ''' <summary>
        ''' Creates a new credential manager instance
        ''' </summary>
        ''' <param name="vStorageMethod">The storage method to use</param>
        Public Sub New(Optional vStorageMethod As eStorageMethod = eStorageMethod.eUnspecified)
            pStorageMethod = vStorageMethod
            
            ' Auto-detect if not specified
            If pStorageMethod = eStorageMethod.eUnspecified Then
                pStorageMethod = DetectAvailableMethod()
            End If
            
            ' Setup encrypted file path as fallback
            Dim lConfigDir As String = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            pCredentialFilePath = Path.Combine(lConfigDir, "SimpleIDE", ".credentials")
            
            ' Generate machine-specific encryption key for file-based storage
            GenerateEncryptionKey()
        End Sub
        
        ' ===== Public Methods =====
        
        ''' <summary>
        ''' Stores a credential securely
        ''' </summary>
        ''' <param name="vService">Service name (e.g., "github.com")</param>
        ''' <param name="vAccount">Account/username</param>
        ''' <param name="vPassword">Password or token to store</param>
        ''' <returns>True if successfully stored</returns>
        Public Function StoreCredential(vService As String, vAccount As String, vPassword As String) As Boolean
            Try
                Select Case pStorageMethod
                    Case eStorageMethod.eGnomeKeyring
                        Return StoreInGnomeKeyring(vService, vAccount, vPassword)
                        
                    Case eStorageMethod.eLibSecret
                        Return StoreInLibSecret(vService, vAccount, vPassword)
                        
                    Case eStorageMethod.eKWallet
                        Return StoreInKWallet(vService, vAccount, vPassword)
                        
                    Case eStorageMethod.eEncryptedFile
                        Return StoreInEncryptedFile(vService, vAccount, vPassword)
                        
                    Case Else
                        Console.WriteLine("No credential storage method available")
                        Return False
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"StoreCredential error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Retrieves a stored credential
        ''' </summary>
        ''' <param name="vService">Service name</param>
        ''' <param name="vAccount">Account/username</param>
        ''' <returns>The password/token or empty string if not found</returns>
        Public Function RetrieveCredential(vService As String, vAccount As String) As String
            Try
                Select Case pStorageMethod
                    Case eStorageMethod.eGnomeKeyring
                        Return RetrieveFromGnomeKeyring(vService, vAccount)
                        
                    Case eStorageMethod.eLibSecret
                        Return RetrieveFromLibSecret(vService, vAccount)
                        
                    Case eStorageMethod.eKWallet
                        Return RetrieveFromKWallet(vService, vAccount)
                        
                    Case eStorageMethod.eEncryptedFile
                        Return RetrieveFromEncryptedFile(vService, vAccount)
                        
                    Case Else
                        Return ""
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"RetrieveCredential error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ''' <summary>
        ''' Deletes a stored credential
        ''' </summary>
        ''' <param name="vService">Service name</param>
        ''' <param name="vAccount">Account/username</param>
        ''' <returns>True if successfully deleted</returns>
        Public Function DeleteCredential(vService As String, vAccount As String) As Boolean
            Try
                Select Case pStorageMethod
                    Case eStorageMethod.eGnomeKeyring
                        Return DeleteFromGnomeKeyring(vService, vAccount)
                        
                    Case eStorageMethod.eLibSecret
                        Return DeleteFromLibSecret(vService, vAccount)
                        
                    Case eStorageMethod.eKWallet
                        Return DeleteFromKWallet(vService, vAccount)
                        
                    Case eStorageMethod.eEncryptedFile
                        Return DeleteFromEncryptedFile(vService, vAccount)
                        
                    Case Else
                        Return False
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"DeleteCredential error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Gets the current storage method
        ''' </summary>
        Public ReadOnly Property StorageMethod As eStorageMethod
            Get
                Return pStorageMethod
            End Get
        End Property
        
        ''' <summary>
        ''' Gets a user-friendly name for the storage method
        ''' </summary>
        Public Function GetStorageMethodName() As String
            Select Case pStorageMethod
                Case eStorageMethod.eGnomeKeyring
                    Return "GNOME Keyring"
                Case eStorageMethod.eLibSecret
                    Return "LibSecret"
                Case eStorageMethod.eKWallet
                    Return "KDE Wallet"
                Case eStorageMethod.eEncryptedFile
                    Return "Encrypted File"
                Case Else
                    Return "None"
            End Select
        End Function
        
        ''' <summary>
        ''' Detects available credential storage methods on the system
        ''' </summary>
        ''' <returns>List of available methods</returns>
        Public Shared Function GetAvailableMethods() As List(Of eStorageMethod)
            Dim lMethods As New List(Of eStorageMethod)
            
            ' Check for secret-tool (GNOME Keyring/LibSecret)
            If IsCommandAvailable("secret-tool") Then
                lMethods.Add(eStorageMethod.eGnomeKeyring)
                lMethods.Add(eStorageMethod.eLibSecret)
            End If
            
            ' Check for kwallet
            If IsCommandAvailable("kwallet-query") OrElse IsCommandAvailable("kwalletcli") Then
                lMethods.Add(eStorageMethod.eKWallet)
            End If
            
            ' Encrypted file is always available as fallback
            lMethods.Add(eStorageMethod.eEncryptedFile)
            
            Return lMethods
        End Function
        
        ' ===== Private Methods - Detection =====
        
        ''' <summary>
        ''' Auto-detects the best available storage method
        ''' </summary>
        Private Function DetectAvailableMethod() As eStorageMethod
            ' Prefer GNOME Keyring if available (most common on GTK systems)
            If IsCommandAvailable("secret-tool") Then
                ' Check if gnome-keyring is running
                If IsProcessRunning("gnome-keyring-daemon") Then
                    Return eStorageMethod.eGnomeKeyring
                Else
                    ' secret-tool is available but keyring might not be running
                    Return eStorageMethod.eLibSecret
                End If
            End If
            
            ' Check for KDE Wallet
            If IsCommandAvailable("kwallet-query") Then
                Return eStorageMethod.eKWallet
            End If
            
            ' Fallback to encrypted file
            Return eStorageMethod.eEncryptedFile
        End Function
        
        ''' <summary>
        ''' Checks if a command is available on the system
        ''' </summary>
        Private Shared Function IsCommandAvailable(vCommand As String) As Boolean
            Try
                Dim lProcess As New Process()
                lProcess.StartInfo.FileName = "which"
                lProcess.StartInfo.Arguments = vCommand
                lProcess.StartInfo.UseShellExecute = False
                lProcess.StartInfo.RedirectStandardOutput = True
                lProcess.StartInfo.CreateNoWindow = True
                
                lProcess.Start()
                Dim lOutput As String = lProcess.StandardOutput.ReadToEnd()
                lProcess.WaitForExit()
                
                Return lProcess.ExitCode = 0 AndAlso Not String.IsNullOrWhiteSpace(lOutput)
                
            Catch ex As Exception
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Checks if a process is running
        ''' </summary>
        Private Function IsProcessRunning(vProcessName As String) As Boolean
            Try
                Dim lProcess As New Process()
                lProcess.StartInfo.FileName = "pgrep"
                lProcess.StartInfo.Arguments = vProcessName
                lProcess.StartInfo.UseShellExecute = False
                lProcess.StartInfo.RedirectStandardOutput = True
                lProcess.StartInfo.CreateNoWindow = True
                
                lProcess.Start()
                lProcess.WaitForExit()
                
                Return lProcess.ExitCode = 0
                
            Catch ex As Exception
                Return False
            End Try
        End Function
        
        ' ===== Private Methods - GNOME Keyring =====
        
        ''' <summary>
        ''' Stores credential in GNOME Keyring using secret-tool
        ''' </summary>
        Private Function StoreInGnomeKeyring(vService As String, vAccount As String, vPassword As String) As Boolean
            Try
                Dim lProcess As New Process()
                lProcess.StartInfo.FileName = "secret-tool"
                lProcess.StartInfo.Arguments = $"store --label='{pApplicationName} - {vService}' service {vService} account {vAccount}"
                lProcess.StartInfo.UseShellExecute = False
                lProcess.StartInfo.RedirectStandardInput = True
                lProcess.StartInfo.RedirectStandardOutput = True
                lProcess.StartInfo.CreateNoWindow = True
                
                lProcess.Start()
                
                ' Write password to stdin
                lProcess.StandardInput.WriteLine(vPassword)
                lProcess.StandardInput.Close()
                
                lProcess.WaitForExit()
                
                Return lProcess.ExitCode = 0
                
            Catch ex As Exception
                Console.WriteLine($"StoreInGnomeKeyring error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Retrieves credential from GNOME Keyring
        ''' </summary>
        Private Function RetrieveFromGnomeKeyring(vService As String, vAccount As String) As String
            Try
                Dim lProcess As New Process()
                lProcess.StartInfo.FileName = "secret-tool"
                lProcess.StartInfo.Arguments = $"lookup service {vService} account {vAccount}"
                lProcess.StartInfo.UseShellExecute = False
                lProcess.StartInfo.RedirectStandardOutput = True
                lProcess.StartInfo.CreateNoWindow = True
                
                lProcess.Start()
                Dim lPassword As String = lProcess.StandardOutput.ReadToEnd().Trim()
                lProcess.WaitForExit()
                
                If lProcess.ExitCode = 0 Then
                    Return lPassword
                Else
                    Return ""
                End If
                
            Catch ex As Exception
                Console.WriteLine($"RetrieveFromGnomeKeyring error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ''' <summary>
        ''' Deletes credential from GNOME Keyring
        ''' </summary>
        Private Function DeleteFromGnomeKeyring(vService As String, vAccount As String) As Boolean
            Try
                Dim lProcess As New Process()
                lProcess.StartInfo.FileName = "secret-tool"
                lProcess.StartInfo.Arguments = $"clear service {vService} account {vAccount}"
                lProcess.StartInfo.UseShellExecute = False
                lProcess.StartInfo.CreateNoWindow = True
                
                lProcess.Start()
                lProcess.WaitForExit()
                
                Return lProcess.ExitCode = 0
                
            Catch ex As Exception
                Console.WriteLine($"DeleteFromGnomeKeyring error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' ===== Private Methods - LibSecret =====
        
        ''' <summary>
        ''' Stores credential using LibSecret (same as GNOME Keyring but different backend)
        ''' </summary>
        Private Function StoreInLibSecret(vService As String, vAccount As String, vPassword As String) As Boolean
            ' LibSecret uses the same secret-tool command
            Return StoreInGnomeKeyring(vService, vAccount, vPassword)
        End Function
        
        Private Function RetrieveFromLibSecret(vService As String, vAccount As String) As String
            Return RetrieveFromGnomeKeyring(vService, vAccount)
        End Function
        
        Private Function DeleteFromLibSecret(vService As String, vAccount As String) As Boolean
            Return DeleteFromGnomeKeyring(vService, vAccount)
        End Function
        
        ' ===== Private Methods - KDE Wallet =====
        
        ''' <summary>
        ''' Stores credential in KDE Wallet
        ''' </summary>
        Private Function StoreInKWallet(vService As String, vAccount As String, vPassword As String) As Boolean
            Try
                ' Using kwallet-query (KDE 5+)
                Dim lKey As String = $"{vService}_{vAccount}"
                Dim lProcess As New Process()
                lProcess.StartInfo.FileName = "kwallet-query"
                lProcess.StartInfo.Arguments = $"-w ""{lKey}"" -f ""{pApplicationName}"" kdewallet"
                lProcess.StartInfo.UseShellExecute = False
                lProcess.StartInfo.RedirectStandardInput = True
                lProcess.StartInfo.CreateNoWindow = True
                
                lProcess.Start()
                lProcess.StandardInput.WriteLine(vPassword)
                lProcess.StandardInput.Close()
                lProcess.WaitForExit()
                
                Return lProcess.ExitCode = 0
                
            Catch ex As Exception
                Console.WriteLine($"StoreInKWallet error: {ex.Message}")
                Return False
            End Try
        End Function
        
        Private Function RetrieveFromKWallet(vService As String, vAccount As String) As String
            Try
                Dim lKey As String = $"{vService}_{vAccount}"
                Dim lProcess As New Process()
                lProcess.StartInfo.FileName = "kwallet-query"
                lProcess.StartInfo.Arguments = $"-r ""{lKey}"" -f ""{pApplicationName}"" kdewallet"
                lProcess.StartInfo.UseShellExecute = False
                lProcess.StartInfo.RedirectStandardOutput = True
                lProcess.StartInfo.CreateNoWindow = True
                
                lProcess.Start()
                Dim lPassword As String = lProcess.StandardOutput.ReadToEnd().Trim()
                lProcess.WaitForExit()
                
                If lProcess.ExitCode = 0 Then
                    Return lPassword
                Else
                    Return ""
                End If
                
            Catch ex As Exception
                Console.WriteLine($"RetrieveFromKWallet error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        Private Function DeleteFromKWallet(vService As String, vAccount As String) As Boolean
            Try
                Dim lKey As String = $"{vService}_{vAccount}"
                Dim lProcess As New Process()
                lProcess.StartInfo.FileName = "kwallet-query"
                lProcess.StartInfo.Arguments = $"-e ""{lKey}"" -f ""{pApplicationName}"" kdewallet"
                lProcess.StartInfo.UseShellExecute = False
                lProcess.StartInfo.CreateNoWindow = True
                
                lProcess.Start()
                lProcess.WaitForExit()
                
                Return lProcess.ExitCode = 0
                
            Catch ex As Exception
                Console.WriteLine($"DeleteFromKWallet error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' ===== Private Methods - Encrypted File =====
        
        ''' <summary>
        ''' Generates a machine-specific encryption key
        ''' </summary>
        Private Sub GenerateEncryptionKey()
            Try
                ' Combine machine-specific data for key generation
                Dim lMachineId As String = GetMachineId()
                Dim lUserName As String = Environment.UserName
                Dim lSeed As String = $"{pApplicationName}_{lMachineId}_{lUserName}"
                
                ' Generate key from seed using SHA256
                Using lSha As SHA256 = SHA256.Create()
                    pEncryptionKey = lSha.ComputeHash(Encoding.UTF8.GetBytes(lSeed))
                End Using
                
            Catch ex As Exception
                Console.WriteLine($"GenerateEncryptionKey error: {ex.Message}")
                ' Fallback to a default key (less secure)
                pEncryptionKey = Encoding.UTF8.GetBytes("SimpleIDE_Default_Key_2024_Linux")
            End Try
        End Sub
        
        ''' <summary>
        ''' Gets the machine ID on Linux
        ''' </summary>
        Private Function GetMachineId() As String
            Try
                ' Try to read /etc/machine-id (systemd systems)
                If File.Exists("/etc/machine-id") Then
                    Return File.ReadAllText("/etc/machine-id").Trim()
                End If
                
                ' Try to read /var/lib/dbus/machine-id (older systems)
                If File.Exists("/var/lib/dbus/machine-id") Then
                    Return File.ReadAllText("/var/lib/dbus/machine-id").Trim()
                End If
                
                ' Fallback to hostname
                Return Environment.MachineName
                
            Catch ex As Exception
                Return "unknown"
            End Try
        End Function
        
        ''' <summary>
        ''' Stores credential in encrypted file
        ''' </summary>
        Private Function StoreInEncryptedFile(vService As String, vAccount As String, vPassword As String) As Boolean
            Try
                ' Load existing credentials
                Dim lCredentials As Dictionary(Of String, String) = LoadEncryptedCredentials()
                
                ' Add or update credential
                Dim lKey As String = $"{vService}|{vAccount}"
                lCredentials(lKey) = vPassword
                
                ' Save encrypted
                Return SaveEncryptedCredentials(lCredentials)
                
            Catch ex As Exception
                Console.WriteLine($"StoreInEncryptedFile error: {ex.Message}")
                Return False
            End Try
        End Function
        
        Private Function RetrieveFromEncryptedFile(vService As String, vAccount As String) As String
            Try
                Dim lCredentials As Dictionary(Of String, String) = LoadEncryptedCredentials()
                Dim lKey As String = $"{vService}|{vAccount}"
                
                If lCredentials.ContainsKey(lKey) Then
                    Return lCredentials(lKey)
                Else
                    Return ""
                End If
                
            Catch ex As Exception
                Console.WriteLine($"RetrieveFromEncryptedFile error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        Private Function DeleteFromEncryptedFile(vService As String, vAccount As String) As Boolean
            Try
                Dim lCredentials As Dictionary(Of String, String) = LoadEncryptedCredentials()
                Dim lKey As String = $"{vService}|{vAccount}"
                
                If lCredentials.ContainsKey(lKey) Then
                    lCredentials.Remove(lKey)
                    Return SaveEncryptedCredentials(lCredentials)
                End If
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"DeleteFromEncryptedFile error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Loads and decrypts credentials from file
        ''' </summary>
        Private Function LoadEncryptedCredentials() As Dictionary(Of String, String)
            Try
                Dim lCredentials As New Dictionary(Of String, String)
                
                If Not File.Exists(pCredentialFilePath) Then
                    Return lCredentials
                End If
                
                ' Read encrypted data
                Dim lEncryptedData() As Byte = File.ReadAllBytes(pCredentialFilePath)
                
                ' Decrypt
                Dim lDecryptedData As String = DecryptData(lEncryptedData)
                
                ' Parse credentials
                If Not String.IsNullOrEmpty(lDecryptedData) Then
                    Dim lLines() As String = lDecryptedData.Split({Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries)
                    For Each lLine In lLines
                        Dim lParts() As String = lLine.Split({"|"c}, 2)
                        If lParts.Length = 2 Then
                            lCredentials(lParts(0)) = lParts(1)
                        End If
                    Next
                End If
                
                Return lCredentials
                
            Catch ex As Exception
                Console.WriteLine($"LoadEncryptedCredentials error: {ex.Message}")
                Return New Dictionary(Of String, String)
            End Try
        End Function
        
        ''' <summary>
        ''' Encrypts and saves credentials to file
        ''' </summary>
        Private Function SaveEncryptedCredentials(vCredentials As Dictionary(Of String, String)) As Boolean
            Try
                ' Build credential string
                Dim lBuilder As New StringBuilder()
                For Each kvp In vCredentials
                    lBuilder.AppendLine($"{kvp.Key}|{kvp.Value}")
                Next
                
                ' Encrypt
                Dim lEncryptedData() As Byte = EncryptData(lBuilder.ToString())
                
                ' Ensure directory exists
                Dim lDir As String = Path.GetDirectoryName(pCredentialFilePath)
                If Not Directory.Exists(lDir) Then
                    Directory.CreateDirectory(lDir)
                End If
                
                ' Save to file with restricted permissions
                File.WriteAllBytes(pCredentialFilePath, lEncryptedData)
                
                ' Set file permissions to 600 (owner read/write only)
                SetFilePermissions(pCredentialFilePath, "600")
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"SaveEncryptedCredentials error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Encrypts data using AES
        ''' </summary>
        Private Function EncryptData(vData As String) As Byte()
            Using lAes As Aes = Aes.Create()
                lAes.Key = pEncryptionKey
                lAes.GenerateIV()
                
                Using lEncryptor As ICryptoTransform = lAes.CreateEncryptor()
                    Dim lDataBytes() As Byte = Encoding.UTF8.GetBytes(vData)
                    Dim lEncrypted() As Byte = lEncryptor.TransformFinalBlock(lDataBytes, 0, lDataBytes.Length)
                    
                    ' Combine IV and encrypted data
                    Dim lResult(lAes.IV.Length + lEncrypted.Length - 1) As Byte
                    Array.Copy(lAes.IV, 0, lResult, 0, lAes.IV.Length)
                    Array.Copy(lEncrypted, 0, lResult, lAes.IV.Length, lEncrypted.Length)
                    
                    Return lResult
                End Using
            End Using
        End Function
        
        ''' <summary>
        ''' Decrypts data using AES
        ''' </summary>
        Private Function DecryptData(vData() As Byte) As String
            Using lAes As Aes = Aes.Create()
                lAes.Key = pEncryptionKey
                
                ' Extract IV from data
                Dim lIV(15) As Byte  ' AES IV is 16 bytes
                Array.Copy(vData, 0, lIV, 0, 16)
                lAes.IV = lIV
                
                ' Extract encrypted data
                Dim lEncrypted(vData.Length - 17) As Byte
                Array.Copy(vData, 16, lEncrypted, 0, vData.Length - 16)
                
                Using lDecryptor As ICryptoTransform = lAes.CreateDecryptor()
                    Dim lDecrypted() As Byte = lDecryptor.TransformFinalBlock(lEncrypted, 0, lEncrypted.Length)
                    Return Encoding.UTF8.GetString(lDecrypted)
                End Using
            End Using
        End Function
        
        ''' <summary>
        ''' Sets file permissions using chmod
        ''' </summary>
        Private Sub SetFilePermissions(vFilePath As String, vPermissions As String)
            Try
                Dim lProcess As New Process()
                lProcess.StartInfo.FileName = "chmod"
                lProcess.StartInfo.Arguments = $"{vPermissions} ""{vFilePath}"""
                lProcess.StartInfo.UseShellExecute = False
                lProcess.StartInfo.CreateNoWindow = True
                
                lProcess.Start()
                lProcess.WaitForExit()
                
            Catch ex As Exception
                Console.WriteLine($"SetFilePermissions error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
