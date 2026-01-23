On Error Resume Next
Dim fso, tempFile, path, file
Set fso = CreateObject("Scripting.FileSystemObject")
tempFile = fso.GetSpecialFolder(2) & "\InformerInstallPath.txt"
If fso.FileExists(tempFile) Then
    Set file = fso.OpenTextFile(tempFile, 1, False, -1)
    path = Trim(file.ReadLine)
    file.Close
    fso.DeleteFile tempFile
    If Len(path) > 0 Then
        Session.Property("INSTALLFOLDER") = path
    End If
End If
