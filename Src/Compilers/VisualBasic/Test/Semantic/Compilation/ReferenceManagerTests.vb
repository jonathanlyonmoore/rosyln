﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class ReferenceManagerTests
        Inherits BasicTestBase

        Private Shared ReadOnly SignedDll As VisualBasicCompilationOptions =
            New VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                                              optimize:=True,
                                              cryptoKeyFile:=SigningTestHelpers.KeyPairFile,
                                              strongNameProvider:=New SigningTestHelpers.VirtualizedStrongNameProvider(ImmutableArray.Create(Of String)()))

        <WorkItem(5483, "DevDiv_Projects/Roslyn")>
        <WorkItem(527917, "DevDiv")>
        <Fact>
        Public Sub ReferenceBinding_SymbolUsed()
            ' Identity: C, Version=1.0.0.0, Culture=neutral, PublicKeyToken=374d0c2befcd8cc9
            Dim v1 = New MetadataImageReference(TestResources.SymbolsTests.General.C1.AsImmutableOrNull())

            ' Identity: C, Version=2.0.0.0, Culture=neutral, PublicKeyToken=374d0c2befcd8cc9
            Dim v2 = New MetadataImageReference(TestResources.SymbolsTests.General.C2.AsImmutableOrNull())

            Dim refSource =
<text>
Public Class D 
    Inherits C
End Class
</text>

            Dim refV1 = CreateCompilationWithMscorlib({refSource.Value}, {v1})
            Dim refV2 = CreateCompilationWithMscorlib({refSource.Value}, {v2})

            Dim testRefSource =
<text>
Public Class E 
    Inherits D '1
End Class

Public Class F 
    Inherits D '2
End Class
</text>

            ' reference asks for a lower version than available:
            Dim testRefV1 = CreateCompilationWithMscorlib({testRefSource.Value}, New MetadataReference() {New VisualBasicCompilationReference(refV1), v2}, compOptions:=OptionsDll)

            ' reference asks for a higher version than available:
            Dim testRefV2 = CreateCompilationWithMscorlib({testRefSource.Value}, New MetadataReference() {New VisualBasicCompilationReference(refV2), v1}, compOptions:=OptionsDll)

            testRefV1.VerifyDiagnostics()

            ' Unlike Dev11 we don't include "through <symbol>" in the message. 
            AssertTheseDiagnostics(testRefV2,
<errors>
BC32207: The project currently contains references to more than one version of 'C', a direct reference to version 1.0.0.0 and an indirect reference to version 2.0.0.0. Change the direct reference to use version 2.0.0.0 (or higher) of C.
    Inherits D '1
             ~
BC32207: The project currently contains references to more than one version of 'C', a direct reference to version 1.0.0.0 and an indirect reference to version 2.0.0.0. Change the direct reference to use version 2.0.0.0 (or higher) of C.
    Inherits D '2
             ~
</errors>)
        End Sub

        <Fact>
        <WorkItem(546080, "DevDiv")>
        Public Sub ReferenceBinding_SymbolNotUsed()
            Dim v1 = New MetadataImageReference(TestResources.SymbolsTests.General.C1.AsImmutableOrNull())
            Dim v2 = New MetadataImageReference(TestResources.SymbolsTests.General.C2.AsImmutableOrNull())

            Dim refSource =
<text>
Public Class D 
End Class
</text>

            Dim refV1 = CreateCompilationWithMscorlib({refSource.Value}, {v1})
            Dim refV2 = CreateCompilationWithMscorlib({refSource.Value}, {v2})

            Dim testRefSource =
<text>
Public Class E 
End Class
</text>

            ' reference asks for a lower version than available:
            Dim testRefV1 = CreateCompilationWithMscorlib({refSource.Value}, New MetadataReference() {New VisualBasicCompilationReference(refV1), v2}, compOptions:=OptionsDll)

            ' reference asks for a higher version than available:
            Dim testRefV2 = CreateCompilationWithMscorlib({refSource.Value}, New MetadataReference() {New VisualBasicCompilationReference(refV2), v1}, compOptions:=OptionsDll)

            testRefV1.VerifyDiagnostics()
            testRefV2.VerifyDiagnostics()
        End Sub

        <Fact>
        Public Sub VersionUnification_MultipleVersions()
            Dim sourceLibV1 =
<compilation name="Lib">
    <file><![CDATA[
[Assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
Public Class C
End Class
]]>
    </file>
</compilation>

            Dim libV1 = CreateCompilationWithMscorlib(sourceLibV1, options:=SignedDll)

            Dim sourceLibV2 =
<compilation name="Lib">
    <file><![CDATA[
[Assembly: System.Reflection.AssemblyVersion("2.0.0.0")]
Public Class C
End Class
]]>
    </file>
</compilation>

            Dim libV2 = CreateCompilationWithMscorlib(sourceLibV1, options:=SignedDll)

            Dim sourceLibV3 =
<compilation name="Lib">
    <file><![CDATA[
[Assembly: System.Reflection.AssemblyVersion("3.0.0.0")]
Public Class C
End Class
]]>
    </file>
</compilation>

            Dim libV3 = CreateCompilationWithMscorlib(sourceLibV3, options:=SignedDll)

            Dim sourceRefLibV2 =
<compilation name="RefLibV2">
    <file><![CDATA[
[Assembly: System.Reflection.AssemblyVersion("2.0.0.0")]                

Public Class R
    Public Field As C
End Class
]]>
    </file>
</compilation>

            Dim refLibV2 = CreateCompilationWithMscorlibAndReferences(
                sourceRefLibV2,
                references:={New VisualBasicCompilationReference(libV2)}, options:=SignedDll)

            Dim sourceMain =
<compilation name="Main">
    <file><![CDATA[
Public Class M
    Public Sub F()
        Dim x = New R()
        System.Console.WriteLine(x.Field)
    End Sub
End Class
]]>
    </file>
</compilation>

            ' higher version should be preferred over lower version regardless of the order of the references

            Dim main13 = CreateCompilationWithMscorlibAndReferences(
                sourceMain,
                references:={New VisualBasicCompilationReference(libV1), New VisualBasicCompilationReference(libV3), New VisualBasicCompilationReference(refLibV2)})

            main13.VerifyDiagnostics()

            Dim main31 = CreateCompilationWithMscorlibAndReferences(
                sourceMain,
                references:={New VisualBasicCompilationReference(libV3), New VisualBasicCompilationReference(libV1), New VisualBasicCompilationReference(refLibV2)})

            main31.VerifyDiagnostics()
        End Sub

        <Fact>
        <WorkItem(529808, "DevDiv"), WorkItem(546080, "DevDiv"), WorkItem(530246, "DevDiv")>
        Public Sub VersionUnification_UseSiteErrors()

            Dim sourceLibV1 =
    <compilation name="Lib">
        <file><![CDATA[
<Assembly: System.Reflection.AssemblyVersion("1.0.0.0")>

Public Class C 
End Class

Public Delegate Sub D()

Public Interface I
End Interface

Public Class SubclassC
    Inherits C
End Class
]]>
        </file>
    </compilation>

            Dim libV1 = CreateCompilationWithMscorlibAndVBRuntime(sourceLibV1, options:=SignedDll)

            Dim sourceLibV2 =
    <compilation name="Lib">
        <file><![CDATA[
<Assembly: System.Reflection.AssemblyVersion("2.0.0.0")>

Public Class C 
End Class

Public Delegate Sub D()

Public Interface I
End Interface
]]>
        </file>
    </compilation>

            Dim libV2 = CreateCompilationWithMscorlibAndVBRuntime(sourceLibV2, options:=SignedDll)

            Dim sourceRefLibV2 =
    <compilation name="RefLibV2">
        <file><![CDATA[
Imports System.Collections.Generic

<Assembly: System.Reflection.AssemblyVersion("2.0.0.0")>

Public Class R 
    Public Field As C

    Public Property [Property] As C

    Public Property Indexer(arg As C) As Integer
        Get
            Return 0
        End Get
        Set(value As Integer)
        End Set
    End Property

    Public Event [Event] As D

    Public Function Method1() As List(Of C)
        Return Nothing
    End Function

    Public Sub Method2(c As List(Of List(Of C))) 
    End Sub

    Public Sub GenericMethod(Of T As I)()
    End Sub
End Class

Public Class R2
    Public Sub New(arg As C)
    End Sub
End Class

Public Class S1
   Inherits List(Of C)

   Public Class Inner
   End Class
End Class

Public Class S2
    Implements I
End Class

Public Class GenericClass(Of T As I)
   Public Class S
   End Class
End Class
]]>
        </file>
    </compilation>

            Dim refLibV2 = CreateCompilationWithMscorlibAndVBRuntimeAndReferences(
                sourceRefLibV2,
                {New VisualBasicCompilationReference(libV2)},
                options:=SignedDll)

            refLibV2.VerifyDiagnostics()

            Dim sourceX =
    <compilation name="X">
        <file><![CDATA[
Imports System.Collections.Generic

<Assembly: System.Reflection.AssemblyVersion("2.0.0.0")>

Public Class P
  Inherits Q
End Class

Public Class Q
  Inherits S2
End Class
]]>
        </file>
    </compilation>

            Dim x = CreateCompilationWithMscorlibAndVBRuntimeAndReferences(
                sourceX,
                {New VisualBasicCompilationReference(refLibV2), New VisualBasicCompilationReference(libV2)},
                options:=SignedDll)

            x.AssertNoDiagnostics()

            Dim sourceMain =
    <compilation name="Main">
        <file><![CDATA[
Public Class M 
    Public Sub F() 
        Dim c = New C()                         ' ok
        Dim r = New R()                         ' ok
        Dim r2 = New R2(Nothing)                ' error: C in parameter
        Dim f = r.Field                         ' error: C in type
        Dim a = r.Property                      ' error: C in return type
        Dim b = r.Indexer(c)                    ' error: C in parameter
                                              
        AddHandler r.Event, Sub()
                            End Sub
        
        Dim m = r.Method1()                     ' error: ~> C in return type
        r.Method2(Nothing)                      ' error: ~> C in parameter
        r.GenericMethod(Of OKImpl)()            ' error: ~> I in constraint
        Dim g = New GenericClass(Of OKImpl).S() ' error: ~> I in constraint
        Dim s1 = New S1()                       ' error: ~> C in base
        Dim s2 = New S2()                       ' error: ~> I in implements
        Dim s3 = New S1.Inner()                 ' error: ~> C in base
        Dim e = New P()                         ' error: P -> Q -> S2 ~> I in implements  
    End Sub
End Class

Public Class Z
  Inherits S2                                   ' error: S2 ~> I in implements 
End Class

Public Class OKImpl
    Implements I
End Class
]]>
        </file>
    </compilation>


            Dim main = CreateCompilationWithMscorlibAndVBRuntimeAndReferences(sourceMain, {New VisualBasicCompilationReference(refLibV2), New VisualBasicCompilationReference(libV1), New VisualBasicCompilationReference(x)})

            CompilationUtils.AssertTheseDiagnostics(main,
<errors>
BC32207: The project currently contains references to more than one version of 'Lib', a direct reference to version 1.0.0.0 and an indirect reference to version 2.0.0.0. Change the direct reference to use version 2.0.0.0 (or higher) of Lib.
        Dim r2 = New R2(Nothing)                ' error: C in parameter
                 ~~~~~~~~~~~~~~~
BC32207: The project currently contains references to more than one version of 'Lib', a direct reference to version 1.0.0.0 and an indirect reference to version 2.0.0.0. Change the direct reference to use version 2.0.0.0 (or higher) of Lib.
        Dim f = r.Field                         ' error: C in type
                ~~~~~~~
BC32207: The project currently contains references to more than one version of 'Lib', a direct reference to version 1.0.0.0 and an indirect reference to version 2.0.0.0. Change the direct reference to use version 2.0.0.0 (or higher) of Lib.
        Dim a = r.Property                      ' error: C in return type
                ~~~~~~~~~~
BC32207: The project currently contains references to more than one version of 'Lib', a direct reference to version 1.0.0.0 and an indirect reference to version 2.0.0.0. Change the direct reference to use version 2.0.0.0 (or higher) of Lib.
        Dim b = r.Indexer(c)                    ' error: C in parameter
                ~~~~~~~~~~~~
BC32207: The project currently contains references to more than one version of 'Lib', a direct reference to version 1.0.0.0 and an indirect reference to version 2.0.0.0. Change the direct reference to use version 2.0.0.0 (or higher) of Lib.
        AddHandler r.Event, Sub()
                   ~~~~~~~
BC32207: The project currently contains references to more than one version of 'Lib', a direct reference to version 1.0.0.0 and an indirect reference to version 2.0.0.0. Change the direct reference to use version 2.0.0.0 (or higher) of Lib.
        Dim m = r.Method1()                     ' error: ~> C in return type
                ~~~~~~~~~~~
BC32207: The project currently contains references to more than one version of 'Lib', a direct reference to version 1.0.0.0 and an indirect reference to version 2.0.0.0. Change the direct reference to use version 2.0.0.0 (or higher) of Lib.
        r.Method2(Nothing)                      ' error: ~> C in parameter
        ~~~~~~~~~~~~~~~~~~
BC32207: The project currently contains references to more than one version of 'Lib', a direct reference to version 1.0.0.0 and an indirect reference to version 2.0.0.0. Change the direct reference to use version 2.0.0.0 (or higher) of Lib.
        r.GenericMethod(Of OKImpl)()            ' error: ~> I in constraint
        ~~~~~~~~~~~~~~~~~~~~~~~~~~~~
BC32207: The project currently contains references to more than one version of 'Lib', a direct reference to version 1.0.0.0 and an indirect reference to version 2.0.0.0. Change the direct reference to use version 2.0.0.0 (or higher) of Lib.
        Dim g = New GenericClass(Of OKImpl).S() ' error: ~> I in constraint
                    ~~~~~~~~~~~~~~~~~~~~~~~
BC32207: The project currently contains references to more than one version of 'Lib', a direct reference to version 1.0.0.0 and an indirect reference to version 2.0.0.0. Change the direct reference to use version 2.0.0.0 (or higher) of Lib.
        Dim s1 = New S1()                       ' error: ~> C in base
                     ~~
BC32207: The project currently contains references to more than one version of 'Lib', a direct reference to version 1.0.0.0 and an indirect reference to version 2.0.0.0. Change the direct reference to use version 2.0.0.0 (or higher) of Lib.
        Dim s2 = New S2()                       ' error: ~> I in implements
                     ~~
BC32207: The project currently contains references to more than one version of 'Lib', a direct reference to version 1.0.0.0 and an indirect reference to version 2.0.0.0. Change the direct reference to use version 2.0.0.0 (or higher) of Lib.
        Dim s3 = New S1.Inner()                 ' error: ~> C in base
                     ~~~~~~~~
BC32207: The project currently contains references to more than one version of 'Lib', a direct reference to version 1.0.0.0 and an indirect reference to version 2.0.0.0. Change the direct reference to use version 2.0.0.0 (or higher) of Lib.
        Dim e = New P()                         ' error: P -> Q -> S2 ~> I in implements  
                    ~
BC32207: The project currently contains references to more than one version of 'Lib', a direct reference to version 1.0.0.0 and an indirect reference to version 2.0.0.0. Change the direct reference to use version 2.0.0.0 (or higher) of Lib.
  Inherits S2                                   ' error: S2 ~> I in implements 
           ~~
</errors>)

        End Sub

        <Fact>
        <WorkItem(546080, "DevDiv"), WorkItem(530296, "DevDiv")>
        Public Sub VersionUnification_UseSiteErrors_Multiple()
            Dim sourceA1 =
<compilation name="A">
    <file><![CDATA[
<Assembly: System.Reflection.AssemblyVersion("1.0.0.0")>
Public Class A
End Class
]]>
    </file>
</compilation>

            Dim a1 = CreateCompilationWithMscorlib(sourceA1, options:=SignedDll)

            Dim sourceA2 =
<compilation name="A">
    <file><![CDATA[
<Assembly: System.Reflection.AssemblyVersion("2.0.0.0")>
Public Class A
End Class
]]>
    </file>
</compilation>

            Dim a2 = CreateCompilationWithMscorlib(sourceA2, options:=SignedDll)

            Dim sourceB1 =
<compilation name="B">
    <file><![CDATA[
<Assembly: System.Reflection.AssemblyVersion("1.0.0.0")>
Public Class B
End Class
]]>
    </file>
</compilation>

            Dim b1 = CreateCompilationWithMscorlib(sourceB1, options:=SignedDll)

            Dim sourceB2 =
<compilation name="B">
    <file><![CDATA[
<Assembly: System.Reflection.AssemblyVersion("2.0.0.0")>
Public Class B
End Class
]]>
    </file>
</compilation>

            Dim b2 = CreateCompilationWithMscorlib(sourceB2, options:=SignedDll)

            Dim sourceRefA1B2 =
<compilation name="RefA1B2">
    <file><![CDATA[
Imports System.Collections.Generic

<Assembly: System.Reflection.AssemblyVersion("1.0.0.0")>

Public Class R 
    Public Dictionary(Of A, B) Dict = New Dictionary(Of A, B)()

    Public Sub Foo(a As A, b As B)
    End Sub
End Class
]]>
    </file>
</compilation>

            Dim refA1B2 = CreateCompilationWithMscorlibAndReferences(
                sourceRefA1B2,
                references:={New VisualBasicCompilationReference(a1), New VisualBasicCompilationReference(b2)},
                options:=SignedDll)

            Dim sourceMain =
<compilation name="Main">
    <file><![CDATA[
Public Class M
    Public Sub F()
        Dim r = New R()
        System.Console.WriteLine(r.Dict)   ' 2 errors
        r.Foo(Nothing, Nothing)            ' 2 errors
    End Sub
End Class
]]>
    </file>
</compilation>

            Dim main = CreateCompilationWithMscorlibAndReferences(sourceMain, references:={New VisualBasicCompilationReference(refA1B2), New VisualBasicCompilationReference(a2), New VisualBasicCompilationReference(b1)})

            main.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_NameNotMember2, "r.Dict").WithArguments("Dict", "R"),
                Diagnostic(ERRID.ERR_SxSIndirectRefHigherThanDirectRef3, "r.Foo(Nothing, Nothing)").WithArguments("B", "2.0.0.0", "1.0.0.0"))
        End Sub

        <Fact>
        <WorkItem(529808, "DevDiv"), WorkItem(546080, "DevDiv")>
        Public Sub VersionUnification_MemberRefsNotRemapped()
            Dim sourceLibV1 =
<compilation name="Lib">
    <file><![CDATA[
<Assembly: System.Reflection.AssemblyVersion("1.0.0.0")>
Public Class C
End Class
]]>
    </file>
</compilation>

            Dim libV1 = CreateCompilationWithMscorlib(sourceLibV1, options:=SignedDll)

            Dim sourceLibV2 =
<compilation name="Lib">
    <file><![CDATA[
<Assembly: System.Reflection.AssemblyVersion("2.0.0.0")>
Public Class C
End Class
]]>
    </file>
</compilation>

            Dim libV2 = CreateCompilationWithMscorlib(sourceLibV2, options:=SignedDll)

            Dim sourceRefLibV1 =
<compilation name="RefLibV1">
    <file><![CDATA[
<Assembly: System.Reflection.AssemblyVersion("1.0.0.0")>
Public Class RC 
    Public C As C
End Class
]]>
    </file>
</compilation>

            Dim refLibV1 = CreateCompilationWithMscorlibAndReferences(
                sourceRefLibV1,
                references:={New VisualBasicCompilationReference(libV1)},
                options:=SignedDll)

            Dim sourceMain =
<compilation name="Main">
    <file>
Public Class M 
    Public Sub F() 
        Dim c2 = New C()        ' AssemblyRef to LibV2
        Dim rc1 = New RC()      ' AssemblyRef to RefLibV1
        Dim c1 = rc1.C          ' AssemblyRef to LibV1
    End Sub
End Class
    </file>
</compilation>

            Dim main = CreateCompilationWithMscorlibAndReferences(
                sourceMain,
                references:={New VisualBasicCompilationReference(refLibV1), New VisualBasicCompilationReference(libV2)})

            ' no warning (unlike C#)
            main.VerifyDiagnostics()

            ' Disable PE verification, it would need .config file with Lib v1 -> Lib v2 binding redirect.
            CompileAndVerify(main, emitOptions:=EmitOptions.CCI, verify:=False, validator:=
                Sub(assembly, _omitted)
                    Dim reader = assembly.GetMetadataReader()
                    Dim refs As List(Of String) = New List(Of String)()

                    For Each assemblyRef In reader.AssemblyReferences
                        Dim row = reader.GetAssemblyReference(assemblyRef)
                        refs.Add(reader.GetString(row.Name) & " " & row.Version.Major & "." & row.Version.Minor)
                    Next

                    AssertEx.SetEqual({"mscorlib 4.0", "RefLibV1 1.0", "Lib 2.0"}, refs)
                End Sub)
        End Sub

        <Fact>
        <WorkItem(546752, "DevDiv")>
        Public Sub VersionUnification_NoPiaMissingCanonicalTypeSymbol()

            Dim sourceLibV1 =
    <compilation name="Lib">
        <file><![CDATA[
<Assembly: System.Reflection.AssemblyVersion("1.0.0.0")>

Public Class A 
End Class
]]>
        </file>
    </compilation>

            Dim libV1 = CreateCompilationWithMscorlibAndVBRuntime(sourceLibV1, options:=SignedDll)

            Dim sourceLibV2 =
    <compilation name="Lib">
        <file><![CDATA[
<Assembly: System.Reflection.AssemblyVersion("2.0.0.0")>

Public Class A
End Class
]]>
        </file>
    </compilation>

            Dim libV2 = CreateCompilationWithMscorlibAndVBRuntime(sourceLibV2, options:=SignedDll)

            Dim sourceRefLibV1 =
    <compilation name="RefLibV1">
        <file><![CDATA[
Imports System.Runtime.InteropServices

Public Class B
    Inherits A

    Public Sub M(i as IB)
    End Sub
End Class

<ComImport>
<Guid("EFA84E98-533E-434E-9581-9205456FBD4D")>
<TypeIdentifier>
Public Interface IB
End Interface
]]>
        </file>
    </compilation>

            Dim refLibV1 = CreateCompilationWithMscorlibAndVBRuntimeAndReferences(sourceRefLibV1, {New VisualBasicCompilationReference(libV1)}, options:=Options.OptionsDll)
            refLibV1.VerifyDiagnostics()

            Dim sourceMain =
    <compilation name="Main">
        <file><![CDATA[
Public Class Test
    Public Sub Main()
        Dim b As New B()
        b.M(Nothing)
    End Sub
End Class
]]>
        </file>
    </compilation>

            ' NOTE: We won't get a nopia type unless we use a PE reference (i.e. source won't work).
            Dim main = CreateCompilationWithMscorlibAndVBRuntimeAndReferences(sourceMain, {New MetadataImageReference(refLibV1.EmitToArray()), New VisualBasicCompilationReference(libV2)})

            CompilationUtils.AssertTheseDiagnostics(main,
<errors>
BC31539: Cannot find the interop type that matches the embedded type 'IB'. Are you missing an assembly reference?
        b.M(Nothing)
        ~~~~~~~~~~~~
</errors>)

        End Sub

        ''' <summary>
        ''' Two Framework identities with unified versions.
        ''' </summary>
        <Fact>
        <WorkItem(546026, "DevDiv"), WorkItem(546169, "DevDiv")>
        Public Sub CS1703ERR_DuplicateImport()
            Dim text = "Namespace N" & vbCrLf & "End Namespace"

            Dim comp = VisualBasicCompilation.Create(
                "DupSignedRefs",
                {VisualBasicSyntaxTree.ParseText(text)},
                {TestReferences.NetFx.v4_0_30319.System, TestReferences.NetFx.v2_0_50727.System},
                OptionsDll.WithAssemblyIdentityComparer(DesktopAssemblyIdentityComparer.Default))

            comp.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_DuplicateReferenceStrong).WithArguments(TestReferences.NetFx.v4_0_30319.System.Display, TestReferences.NetFx.v2_0_50727.System.Display))
        End Sub

        <WorkItem(545062, "DevDiv")>
        <Fact()>
        Public Sub DuplicateReferences()
            Dim c As VisualBasicCompilation
            Dim source As String
            Dim r1 = New MetadataImageReference(TestResources.SymbolsTests.General.C1.AsImmutableOrNull(), fullPath:="c:\temp\a.dll", display:="R1")
            Dim r2 = New MetadataImageReference(TestResources.SymbolsTests.General.C1.AsImmutableOrNull(), fullPath:="c:\temp\a.dll", display:="R2")
            Dim rEmbed = r1.WithEmbedInteropTypes(True)

            source =
<text>
Class D
End Class
</text>.Value

            c = CreateCompilationWithMscorlib({source}, {r1, r2}, compOptions:=OptionsDll)
            c.AssertTheseDiagnostics()
            Assert.Null(c.GetReferencedAssemblySymbol(r1))
            Assert.NotNull(c.GetReferencedAssemblySymbol(r2))

            source =
<text>
Class D
  Dim x As C
End Class
</text>.Value

            c = CreateCompilationWithMscorlib({source}, {r1, r2}, compOptions:=OptionsDll)
            Assert.Null(c.GetReferencedAssemblySymbol(r1))
            Assert.NotNull(c.GetReferencedAssemblySymbol(r2))
            c.AssertTheseDiagnostics()

            c = CreateCompilationWithMscorlib({source}, {r1, rEmbed}, compOptions:=OptionsDll)
            c.AssertTheseDiagnostics(<errors>
BC31549: Cannot embed interop types from assembly 'C, Version=1.0.0.0, Culture=neutral, PublicKeyToken=374d0c2befcd8cc9' because it is missing the 'System.Runtime.InteropServices.GuidAttribute' attribute.
BC31553: Cannot embed interop types from assembly 'C, Version=1.0.0.0, Culture=neutral, PublicKeyToken=374d0c2befcd8cc9' because it is missing either the 'System.Runtime.InteropServices.ImportedFromTypeLibAttribute' attribute or the 'System.Runtime.InteropServices.PrimaryInteropAssemblyAttribute' attribute.
BC31541: Reference to class 'C' is not allowed when its assembly is configured to embed interop types.
  Dim x As C
           ~
</errors>)
            Assert.Null(c.GetReferencedAssemblySymbol(r1))
            Assert.NotNull(c.GetReferencedAssemblySymbol(rEmbed))

            c = CreateCompilationWithMscorlib({source}, {rEmbed, r1}, compOptions:=OptionsDll)
            c.AssertTheseDiagnostics()
            Assert.Null(c.GetReferencedAssemblySymbol(rEmbed))
            Assert.NotNull(c.GetReferencedAssemblySymbol(r1))
        End Sub

        <WorkItem(539495, "DevDiv")>
        <Fact>
        Public Sub BC32208ERR_DuplicateReference2()
            Using MetadataCache.LockAndClean
                Dim sourceLibV1 =
<compilation name="Lib">
    <file><![CDATA[
Imports System.Reflection
<Assembly: AssemblyVersion("1.0.0.0")>

Public Class  C 
End class
]]>
    </file>
</compilation>

                Dim sourceLibV2 =
<compilation name="Lib">
    <file><![CDATA[
Imports System.Reflection
<Assembly: AssemblyVersion("2.0.0.0")>

Public Class  C 
End class
]]>
    </file>
</compilation>

                Dim sourceRefLibV1 =
<compilation name="RefLibV1">
    <file>
Public Class P
    Dim x = new C()
End Class
    </file>
</compilation>

                Dim sourceMain =
<compilation name="Main">
    <file>        
Class Q
    Dim x = new P()
End Class
    </file>
</compilation>

                '
                ' test duplicate references in assemblies from bytes
                '
                Dim libV1 = CompilationUtils.CreateCompilationWithMscorlib(sourceLibV1, OutputKind.DynamicallyLinkedLibrary)
                Dim imageLibV1 = libV1.EmitToArray()
                Dim metadataLibV1 = New MetadataImageReference(imageLibV1)

                Dim refLibV1 = CompilationUtils.CreateCompilationWithMscorlibAndReferences(sourceRefLibV1, {metadataLibV1})
                Dim imageRefLibV1 = refLibV1.EmitToArray()

                Dim main = CompilationUtils.CreateCompilationWithMscorlibAndReferences(sourceMain,
                {metadataLibV1,
                 New MetadataImageReference(imageRefLibV1),
                 New MetadataImageReference(imageRefLibV1, display:="MyBytesAssembly1")})

                CompilationUtils.AssertTheseDiagnostics(main,
<expected>
BC32208: Project already has a reference to assembly 'RefLibV1'. A second reference to 'MyBytesAssembly1' cannot be added.
</expected>)

                '
                ' test duplicate references in assemblies from file assemblies
                '
                Dim tempFile1_copy1 = Temp.CreateFile("Lib", ".dll").WriteAllBytes(imageLibV1)
                Dim tempFile1_copy2 = Temp.CreateFile("Lib", ".dll").WriteAllBytes(imageLibV1)
                Dim tempFile2 = Temp.CreateFile("Lib", ".dll").WriteAllBytes(imageRefLibV1)

                main = CompilationUtils.CreateCompilationWithMscorlibAndReferences(sourceMain, {
                    New MetadataFileReference(tempFile1_copy1.Path),
                    New MetadataFileReference(tempFile2.Path),
                    New MetadataFileReference(tempFile1_copy2.Path)}, OptionsDll)

                CompilationUtils.AssertTheseDiagnostics(main,
<expected>
BC32208: Project already has a reference to assembly 'Lib'. A second reference to '<%= tempFile1_copy2.Path %>' cannot be added.
</expected>)

                '
                ' no error is reported if normalized paths are the same:
                '
                main = CompilationUtils.CreateCompilationWithMscorlibAndReferences(sourceMain, {
                    New MetadataFileReference(tempFile1_copy1.Path),
                    New MetadataFileReference(tempFile2.Path),
                    New MetadataFileReference(tempFile1_copy1.Path)}, OptionsDll)

                main.VerifyDiagnostics()
                Dim compRef1Copy = libV1.Clone()

                '
                ' test duplicate references in assemblies from compilations
                '
                main = CompilationUtils.CreateCompilationWithMscorlibAndReferences(sourceMain,
                {
                    New VisualBasicCompilationReference(libV1),
                    New VisualBasicCompilationReference(refLibV1),
                    New VisualBasicCompilationReference(compRef1Copy)
                }, OptionsDll)

                CompilationUtils.AssertTheseDiagnostics(main,
<expected>
BC32208: Project already has a reference to assembly 'Lib'. A second reference to 'Lib' cannot be added.
</expected>)

                ' test duplicate references in assemblies from compilations do not show an error if the assembly has a strong name
                DirectCast(libV1.Assembly, SourceAssemblySymbol).m_lazyIdentity = New AssemblyIdentity(libV1.AssemblyName, New Version("4.3.2.1"), publicKeyOrToken:=New Byte() {0, 1, 2, 3, 4, 5, 6, 7}.AsImmutableOrNull())
                main = CompilationUtils.CreateCompilationWithMscorlibAndReferences(sourceMain, {New VisualBasicCompilationReference(libV1), New VisualBasicCompilationReference(refLibV1), New VisualBasicCompilationReference(libV1)}, OptionsDll)
                CompilationUtils.AssertNoErrors(main)
            End Using
        End Sub

        <Fact>
        Public Sub WeakIdentitiesWithDifferentVersions()
            Dim sourceLibV1 =
<compilation name="Lib">
    <file><![CDATA[
Imports System.Reflection
<Assembly: AssemblyVersion("1.0.0.0")>

Public Class C1
End class
]]>
    </file>
</compilation>

            Dim sourceLibV2 =
<compilation name="Lib">
    <file><![CDATA[
Imports System.Reflection
<Assembly: AssemblyVersion("2.0.0.0")>

Public Class C2 
End class
]]>
    </file>
</compilation>

            Dim sourceRefLibV1 =
<compilation name="RefLibV1">
    <file>
Public Class P
    Dim x As C1
End Class
    </file>
</compilation>

            Dim sourceMain =
<compilation name="Main">
    <file>        
Public Class Q
    Dim x As P
    Dim y As C1
    Dim z As C2
End Class
    </file>
</compilation>

            Dim libV1 = CompilationUtils.CreateCompilationWithMscorlib(sourceLibV1)
            Dim libV2 = CompilationUtils.CreateCompilationWithMscorlib(sourceLibV2)

            Dim refLibV1 = CompilationUtils.CreateCompilationWithMscorlibAndReferences(sourceRefLibV1,
                {New VisualBasicCompilationReference(libV1)})

            Dim main = CompilationUtils.CreateCompilationWithMscorlibAndReferences(sourceMain,
                {New VisualBasicCompilationReference(libV1), New VisualBasicCompilationReference(refLibV1), New VisualBasicCompilationReference(libV2)})

            main.VerifyDiagnostics()
        End Sub

        ''' <summary>
        ''' Although the CLR considers all WinRT references equivalent the Dev11 C# and VB compilers still 
        ''' compare their identities as if they were regular managed dlls.
        ''' </summary>
        <Fact>
        Public Sub WinMd_SameSimpleNames_SameVersions()
            Dim sourceMain =
<compilation name="Main">
    <file>        
Public Class Q
    Dim y As C1
    Dim z As C2
End Class
    </file>
</compilation>
            ' W1.dll: (W, Version=255.255.255.255, Culture=null, PKT=null) 
            ' W2.dll: (W, Version=255.255.255.255, Culture=null, PKT=null) 

            Using metadataLib1 = AssemblyMetadata.CreateFromImage(TestResources.WinRt.W1.AsImmutable()),
                  metadataLib2 = AssemblyMetadata.CreateFromImage(TestResources.WinRt.W2.AsImmutable())

                Dim mdRefLib1 = New MetadataImageReference(metadataLib1, fullPath:="C:\W1.dll")
                Dim mdRefLib2 = New MetadataImageReference(metadataLib2, fullPath:="C:\W2.dll")

                Dim main = CompilationUtils.CreateCompilationWithMscorlibAndReferences(sourceMain,
                    {mdRefLib1, mdRefLib2})

                main.VerifyDiagnostics(
                    Diagnostic(ERRID.ERR_DuplicateReference2).WithArguments("W", "C:\W2.dll"),
                    Diagnostic(ERRID.ERR_UndefinedType1, "C1").WithArguments("C1"))
            End Using
        End Sub

        ''' <summary>
        ''' Although the CLR considers all WinRT references equivalent the Dev11 C# and VB compilers still 
        ''' compare their identities as if they were regular managed dlls.
        ''' </summary>
        <Fact>
        Public Sub WinMd_DifferentSimpleNames()
            Dim sourceMain =
<compilation name="Main">
    <file>        
Public Class Q
    Dim y As C1
    Dim z As CB
End Class
    </file>
</compilation>
            ' W1.dll: (W, Version=255.255.255.255, Culture=null, PKT=null) 
            ' WB.dll: (WB, Version=255.255.255.255, Culture=null, PKT=null) 

            Using metadataLib1 = AssemblyMetadata.CreateFromImage(TestResources.WinRt.W1.AsImmutable()),
                  metadataLib2 = AssemblyMetadata.CreateFromImage(TestResources.WinRt.WB.AsImmutable())

                Dim mdRefLib1 = New MetadataImageReference(metadataLib1, fullPath:="C:\W1.dll")
                Dim mdRefLib2 = New MetadataImageReference(metadataLib2, fullPath:="C:\WB.dll")

                Dim main = CompilationUtils.CreateCompilationWithMscorlibAndReferences(sourceMain,
                    {mdRefLib1, mdRefLib2})

                main.VerifyDiagnostics()
            End Using
        End Sub

        ''' <summary>
        ''' Although the CLR considers all WinRT references equivalent the Dev11 C# and VB compilers still 
        ''' compare their identities as if they were regular managed dlls.
        ''' </summary>
        <Fact>
        Public Sub WinMd_SameSimpleNames_DifferentVersions()
            Dim sourceMain =
<compilation name="Main">
    <file>        
Public Class Q
    Dim y As CB
    Dim z As CB_V1
End Class
    </file>
</compilation>
            ' WB.dll:          (WB, Version=255.255.255.255, Culture=null, PKT=null) 
            ' WB_Version1.dll: (WB, Version=1.0.0.0, Culture=null, PKT=null) 

            Using metadataLib1 = AssemblyMetadata.CreateFromImage(TestResources.WinRt.WB.AsImmutable()),
                  metadataLib2 = AssemblyMetadata.CreateFromImage(TestResources.WinRt.WB_Version1.AsImmutable())

                Dim mdRefLib1 = New MetadataImageReference(metadataLib1, fullPath:="C:\WB.dll")
                Dim mdRefLib2 = New MetadataImageReference(metadataLib2, fullPath:="C:\WB_Version1.dll")

                Dim main = CompilationUtils.CreateCompilationWithMscorlibAndReferences(sourceMain,
                    {mdRefLib1, mdRefLib2})

                main.VerifyDiagnostics()
            End Using
        End Sub

        ''' <summary> 
        ''' We replicate the Dev11 behavior here but is there any real world scenario for this? 
        ''' </summary>
        <Fact>
        Public Sub MetadataReferencesDifferInCultureOnly()
            Dim arSA = TestReferences.SymbolsTests.Versioning.AR_SA
            Dim enUS = TestReferences.SymbolsTests.Versioning.EN_US

            Dim source =
<compilation name="Main">
    <file>        
Public Class A 
   Public arSA a = New arSA()
   Public enUS b = New enUS()
End Class
    </file>
</compilation>

            Dim compilation = CreateCompilationWithMscorlibAndReferences(source, {arSA, enUS})
            Dim arSA_sym = compilation.GetReferencedAssemblySymbol(arSA)
            Dim enUS_sym = compilation.GetReferencedAssemblySymbol(enUS)

            ' the last one wins
            Assert.Equal(Nothing, arSA_sym)
            Assert.Equal("en-US", enUS_sym.Identity.CultureName)

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_DuplicateReference2).WithArguments("Culture", "EN-US"),
                Diagnostic(ERRID.ERR_ExpectedEOS, "a"),
                Diagnostic(ERRID.ERR_ExpectedEOS, "b"))
        End Sub

        <Fact>
        Public Sub CyclesInReferences()
            Dim sourceA =
<compilation name="A">
    <file>        
Public Class A
End Class
    </file>
</compilation>

            Dim a = CreateCompilationWithMscorlib(sourceA)
            a.VerifyDiagnostics()

            Dim sourceB =
<compilation name="B">
    <file>        
Public Class B 
    Inherits A
End Class

Public Class Foo
End Class
    </file>
</compilation>

            Dim b = CreateCompilationWithMscorlibAndReferences(sourceB, {New VisualBasicCompilationReference(a)})
            b.VerifyDiagnostics()
            Dim refB = New MetadataImageReference(b.EmitToArray(), display:="B")

            ' construct A2 that has a reference to assembly identity "B".
            Dim sourceA2 =
<compilation name="A">
    <file>        
Public Class A 
    Public x As Foo = New Foo()
End Class
    </file>
</compilation>

            Dim a2 = CreateCompilationWithMscorlibAndReferences(sourceA2, {refB})
            a2.VerifyDiagnostics()
            Dim refA2 = New MetadataImageReference(a2.EmitToArray(), display:="A2")
            Dim symbolB = a2.GetReferencedAssemblySymbol(refB)
            Assert.True(TypeOf symbolB Is VisualBasic.Symbols.Metadata.PE.PEAssemblySymbol, "PE symbol expected")

            ' force A assembly symbol to be added to a metadata cache:
            Dim sourceC =
<compilation name="C">
    <file>
Class C 
    Inherits A 
End Class
    </file>
</compilation>

            Dim c = CreateCompilationWithMscorlibAndReferences(sourceC, {refA2, refB})
            c.VerifyDiagnostics()
            Dim symbolA2 = c.GetReferencedAssemblySymbol(refA2)
            Assert.True(TypeOf symbolA2 Is VisualBasic.Symbols.Metadata.PE.PEAssemblySymbol, "PE symbol expected")
            Assert.Equal(1, (DirectCast(refA2.GetMetadata(), AssemblyMetadata)).CachedSymbols.WeakCount)

            GC.KeepAlive(symbolA2)

            ' Recompile "B" and remove int Foo. The assembly manager should not reuse symbols for A since they are referring to old version of B.
            Dim sourceB2 =
<compilation name="B">
    <file>
Public Class B 
    Inherits A 

    Public Sub Bar
       Dim objX As Object = Me.x
    End Sub
End Class
    </file>
</compilation>

            Dim b2 = CreateCompilationWithMscorlibAndReferences(sourceB2, {refA2})

            ' TODO (tomat): Dev11 reports error:
            ' error BC30652: Reference required to assembly 'b, Version=1.0.0.0, Culture=neutral, PublicKeyToken = null' containing the type 'Foo'. Add one to your project.

            AssertTheseDiagnostics(b2,
<errors>
BC31091: Import of type 'Foo' from assembly or module 'B.dll' failed.
       Dim objX As Object = Me.x
                            ~~~~
</errors>)
        End Sub

        <Fact>
        Public Sub BoundReferenceCaching_CyclesInReferences()
            Dim sourceA =
<compilation name="A">
    <file>        
Public Class A
End Class
    </file>
</compilation>

            Dim sourceB =
<compilation name="B">
    <file>        
Public Class B 
    Inherits A
End Class
    </file>
</compilation>

            Dim sourceA2 =
<compilation name="A">
    <file>        
Public Class A
    Dim x As B
End Class
    </file>
</compilation>

            Dim a = CreateCompilationWithMscorlib(sourceA)
            Dim b = CreateCompilationWithMscorlibAndReferences(sourceB, {New VisualBasicCompilationReference(a)})
            Dim refB = New MetadataImageReference(b.EmitToArray())

            ' construct A2 that has a reference to assembly identity "B".
            Dim a2 = CreateCompilationWithMscorlibAndReferences(sourceA2, {refB})
            Dim refA2 = New MetadataImageReference(a2.EmitToArray())

            Dim withCircularReference1 = CreateCompilationWithMscorlibAndReferences(sourceB, {refA2})
            Dim withCircularReference2 = withCircularReference1.WithOptions(OptionsDll.WithMainTypeName("Blah"))
            Assert.NotSame(withCircularReference1, withCircularReference2)

            ' until we try to reuse bound references we share the manager:
            Assert.True(withCircularReference1.ReferenceManagerEquals(withCircularReference2))

            Dim assembly1 = withCircularReference1.SourceAssembly
            Assert.True(withCircularReference1.ReferenceManagerEquals(withCircularReference2))

            Dim assembly2 = withCircularReference2.SourceAssembly
            Assert.False(withCircularReference1.ReferenceManagerEquals(withCircularReference2))

            Dim refA2_symbol1 = withCircularReference1.GetReferencedAssemblySymbol(refA2)
            Dim refA2_symbol2 = withCircularReference2.GetReferencedAssemblySymbol(refA2)

            Assert.NotNull(refA2_symbol1)
            Assert.NotNull(refA2_symbol2)
            Assert.NotSame(refA2_symbol1, refA2_symbol2)
        End Sub

        <Fact(), WorkItem(530795, "DevDiv")>
        Public Sub ReferenceTwoVersionsOfSystem()
            Dim compilation = CreateCompilationWithReferences(
                <compilation>
                    <file name="a.vb">
Module Module1
    Sub Main()
        Dim client As System.Net.Sockets.TcpClient = Nothing
    End Sub
End Module

                    </file>
                </compilation>,
                references:={MscorlibRef, MsvbRef, SystemRef, SystemRef_v20},
                options:=OptionsDll.WithAssemblyIdentityComparer(DesktopAssemblyIdentityComparer.Default))

            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_DuplicateReferenceStrong).WithArguments("System.v4_0_30319.dll", "System.v2_0_50727.dll"))
        End Sub

        ' NOTE: This does not work in dev11, but the code is shared with C# so there's
        ' no reason not to implement it in roslyn.
        <Fact, WorkItem(546828, "DevDiv")>
        Public Sub MetadataDependsOnSource()
            ' {0} is the body of the ReachFramework assembly reference.
            Dim ilTemplate = <![CDATA[
.assembly extern ReachFramework
{{
{0}
}}
.assembly extern mscorlib
{{
  .publickeytoken = (B7 7A 5C 56 19 34 E0 89 )                         // .z\V.4..
  .ver 4:0:0:0
}}
.assembly PresentationFramework
{{
  .publickey = (00 24 00 00 04 80 00 00 94 00 00 00 06 02 00 00   // .$..............
                00 24 00 00 52 53 41 31 00 04 00 00 01 00 01 00   // .$..RSA1........
                B5 FC 90 E7 02 7F 67 87 1E 77 3A 8F DE 89 38 C8   // ......g..w:...8.
                1D D4 02 BA 65 B9 20 1D 60 59 3E 96 C4 92 65 1E   // ....e. .`Y>...e.
                88 9C C1 3F 14 15 EB B5 3F AC 11 31 AE 0B D3 33   // ...?....?..1...3
                C5 EE 60 21 67 2D 97 18 EA 31 A8 AE BD 0D A0 07   // ..`!g-...1......
                2F 25 D8 7D BA 6F C9 0F FD 59 8E D4 DA 35 E4 4C   // /%.}}.o...Y...5.L
                39 8C 45 43 07 E8 E3 3B 84 26 14 3D AE C9 F5 96   // 9.EC...;.&.=....
                83 6F 97 C8 F7 47 50 E5 97 5C 64 E2 18 9F 45 DE   // .o...GP..\d...E.
                F4 6B 2A 2B 12 47 AD C3 65 2B F5 C3 08 05 5D A9 ) // .k*+.G..e+....].
  .ver 4:0:0:0
}}

.module PresentationFramework.dll
// MVID: {{CBA9159C-5BB4-49BC-B41D-AF055BF1C0AB}}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x04D00000


// =============== CLASS MEMBERS DECLARATION ===================

.class public auto ansi System.Windows.Controls.PrintDialog
       extends [mscorlib]System.Object
{{
  .method public hidebysig instance class [ReachFramework]System.Printing.PrintTicket 
          Test() cil managed
  {{
    ret
  }}

  .method public hidebysig specialname rtspecialname 
          instance void  .ctor() cil managed
  {{
    ret
  }}
}}
]]>

            Dim vb =
                <compilation name="ReachFramework">
                    <file name="a.vb">
Imports System.Windows.Controls

Namespace System.Printing
    Public Class PrintTicket

    End Class
End Namespace

Module Test
    Sub main()
        Dim dialog As New PrintDialog()
        Dim p = dialog.Test()
    End Sub
End Module
                    </file>
                </compilation>

            ' ref only specifies name
            If True Then
                Dim il = String.Format(ilTemplate.Value, "")
                Dim ilRef = CompileIL(il, appendDefaultHeader:=False)
                CreateCompilationWithMscorlibAndVBRuntimeAndReferences(vb, {ilRef}).VerifyDiagnostics()
            End If

            ' public key specified by ref, but not def
            If True Then
                Dim il = String.Format(ilTemplate.Value, "  .publickeytoken = (31 BF 38 56 AD 36 4E 35 )                         // 1.8V.6N5")
                Dim ilRef = CompileIL(il, appendDefaultHeader:=False)
                CreateCompilationWithMscorlibAndVBRuntimeAndReferences(vb, {ilRef}).VerifyDiagnostics()
            End If

            ' version specified by ref, but not def
            If True Then
                Dim il = String.Format(ilTemplate.Value, "  .ver 4:0:0:0")
                Dim ilRef = CompileIL(il, appendDefaultHeader:=False)
                CreateCompilationWithMscorlibAndVBRuntimeAndReferences(vb, {ilRef}).VerifyDiagnostics()
            End If

            ' culture specified by ref, but not def
            If True Then
                Dim il = String.Format(ilTemplate.Value, "  .locale = (65 00 6E 00 2D 00 63 00 61 00 00 00 )             // e.n.-.c.a...")
                Dim ilRef = CompileIL(il, appendDefaultHeader:=False)
                CreateCompilationWithMscorlibAndVBRuntimeAndReferences(vb, {ilRef}).VerifyDiagnostics()
            End If
        End Sub

        ' NOTE: This does not work in dev11, but the code is shared with C# so there's
        ' no reason not to implement it in roslyn.
        <Fact, WorkItem(546828, "DevDiv")>
        Public Sub MetadataDependsOnMetadataOrSource()
            Dim il = <![CDATA[
.assembly extern ReachFramework
{
  .ver 4:0:0:0
}
.assembly extern mscorlib
{
  .publickeytoken = (B7 7A 5C 56 19 34 E0 89 )                         // .z\V.4..
  .ver 4:0:0:0
}
.assembly PresentationFramework
{
  .publickey = (00 24 00 00 04 80 00 00 94 00 00 00 06 02 00 00   // .$..............
                00 24 00 00 52 53 41 31 00 04 00 00 01 00 01 00   // .$..RSA1........
                B5 FC 90 E7 02 7F 67 87 1E 77 3A 8F DE 89 38 C8   // ......g..w:...8.
                1D D4 02 BA 65 B9 20 1D 60 59 3E 96 C4 92 65 1E   // ....e. .`Y>...e.
                88 9C C1 3F 14 15 EB B5 3F AC 11 31 AE 0B D3 33   // ...?....?..1...3
                C5 EE 60 21 67 2D 97 18 EA 31 A8 AE BD 0D A0 07   // ..`!g-...1......
                2F 25 D8 7D BA 6F C9 0F FD 59 8E D4 DA 35 E4 4C   // /%.}.o...Y...5.L
                39 8C 45 43 07 E8 E3 3B 84 26 14 3D AE C9 F5 96   // 9.EC...;.&.=....
                83 6F 97 C8 F7 47 50 E5 97 5C 64 E2 18 9F 45 DE   // .o...GP..\d...E.
                F4 6B 2A 2B 12 47 AD C3 65 2B F5 C3 08 05 5D A9 ) // .k*+.G..e+....].
  .ver 4:0:0:0
}

.module PresentationFramework.dll
// MVID: {CBA9159C-5BB4-49BC-B41D-AF055BF1C0AB}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x04D00000


// =============== CLASS MEMBERS DECLARATION ===================

.class public auto ansi System.Windows.Controls.PrintDialog
       extends [mscorlib]System.Object
{
  .method public hidebysig instance class [ReachFramework]System.Printing.PrintTicket 
          Test() cil managed
  {
    ret
  }

  .method public hidebysig specialname rtspecialname 
          instance void  .ctor() cil managed
  {
    ret
  }
}
]]>

            Dim oldVb =
                <compilation name="ReachFramework">
                    <file name="a.vb">
&lt;assembly: System.Reflection.AssemblyVersion("1.0.0.0")&gt;

Namespace System.Printing
    Public Class PrintTicket

    End Class
End Namespace
                    </file>
                </compilation>

            Dim newVb =
                <compilation name="ReachFramework">
                    <file name="a.vb">
&lt;assembly: System.Reflection.AssemblyVersion("4.0.0.0")&gt;

Namespace System.Printing
    Public Class PrintTicket

    End Class
End Namespace
                    </file>
                </compilation>


            Dim ilRef = CompileIL(il.Value, appendDefaultHeader:=False)
            Dim oldRef = CreateCompilationWithMscorlibAndVBRuntime(oldVb).ToMetadataReference()

            Dim comp = CreateCompilationWithMscorlibAndVBRuntimeAndReferences(newVb, {ilRef, oldRef})
            comp.VerifyDiagnostics()

            Dim method = comp.GlobalNamespace.
                GetMember(Of NamespaceSymbol)("System").
                GetMember(Of NamespaceSymbol)("Windows").
                GetMember(Of NamespaceSymbol)("Controls").
                GetMember(Of NamedTypeSymbol)("PrintDialog").
                GetMember(Of MethodSymbol)("Test")

            Dim actualIdentity As AssemblyIdentity = method.ReturnType.ContainingAssembly.Identity

            ' Even though the compilation has the correct version number, the referenced binary is preferred.
            Assert.Equal(oldRef.Compilation.Assembly.Identity, actualIdentity)
            Assert.NotEqual(comp.Assembly.Identity, actualIdentity)
        End Sub

        <Fact(), WorkItem(530303, "DevDiv")>
        Public Sub TestReferenceResolution()
            Dim vb1Compilation = CreateVisualBasicCompilation("VB1",
            <![CDATA[Public Class VB1
End Class]]>,
                compilationOptions:=New VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            Dim vb1Verifier = CompileAndVerify(vb1Compilation)
            vb1Verifier.VerifyDiagnostics()

            Dim vb2Compilation = CreateVisualBasicCompilation("VB2",
            <![CDATA[Public Class VB2(Of T)
End Class]]>,
                compilationOptions:=New VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                referencedCompilations:={vb1Compilation})
            Dim vb2Verifier = CompileAndVerify(vb2Compilation)
            vb2Verifier.VerifyDiagnostics()

            Dim vb3Compilation = CreateVisualBasicCompilation("VB3",
            <![CDATA[Public Class VB3 : Inherits VB2(Of VB1)
End Class]]>,
                compilationOptions:=New VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                referencedCompilations:={vb1Compilation, vb2Compilation})
            Dim vb3Verifier = CompileAndVerify(vb3Compilation)
            vb3Verifier.VerifyDiagnostics()

            Dim vb4Compilation = CreateVisualBasicCompilation("VB4",
            <![CDATA[Public Module Program
    Sub Main()
        System.Console.WriteLine(GetType(VB3))
    End Sub
End Module]]>,
                compilationOptions:=New VisualBasicCompilationOptions(OutputKind.ConsoleApplication),
                referencedCompilations:={vb2Compilation, vb3Compilation})
            vb4Compilation.VerifyDiagnostics()
        End Sub

        <Fact(), WorkItem(531537, "DevDiv")>
        Public Sub ModuleSymbolReuse()
            Dim assemblySource =
<compilation name="lib1">
    <file name="lib1.vb">
Public Class TypeDependedOnByModule
End Class
    </file>
</compilation>

            Dim moduleSource =
    <compilation name="lib2">
        <file name="lib2.vb">
Public Class TypeFromModule
    Inherits TypeDependedOnByModule
End Class
        </file>
    </compilation>

            ' Note: we just need *a* module reference for the repro - we're not depending on its contents, name, etc.
            Dim assemblyMd = AssemblyMetadata.CreateFromImage(CreateCompilationWithMscorlib(assemblySource).EmitToArray())
            Dim moduleRef = CreateCompilationWithMscorlibAndReferences(moduleSource, {New MetadataImageReference(assemblyMd)}, Options.OptionsNetModule).EmitToImageReference()

            Dim text1 =
    <compilation name="test">
        <file name="test.vb">
Public Class A
    Function M() As TypeFromModule
        Return Nothing
    End Function
End Class
        </file>
    </compilation>

            Dim comp1 = CreateCompilationWithMscorlibAndReferences(text1, {moduleRef})
            Dim tree1 = comp1.SyntaxTrees.Single()

            Dim moduleSymbol1 = comp1.GetReferencedModuleSymbol(moduleRef)
            Assert.Equal(comp1.Assembly, moduleSymbol1.ContainingAssembly)

            Dim moduleReferences1 = moduleSymbol1.GetReferencedAssemblies()
            Assert.Contains(assemblyMd.Assembly.Identity, moduleReferences1.AsEnumerable())

            Dim moduleTypeSymbol1 = comp1.GlobalNamespace.GetMember(Of NamedTypeSymbol)("TypeFromModule")
            Assert.Equal(moduleSymbol1, moduleTypeSymbol1.ContainingModule)
            Assert.Equal(comp1.Assembly, moduleTypeSymbol1.ContainingAssembly)

            Dim tree2 = tree1.WithInsertAt(tree1.ToString().Length, "'Comment")
            Dim comp2 = comp1.ReplaceSyntaxTree(tree1, tree2)

            Dim moduleSymbol2 = comp2.GetReferencedModuleSymbol(moduleRef)
            Assert.Equal(comp2.Assembly, moduleSymbol2.ContainingAssembly)

            Dim moduleReferences2 = moduleSymbol2.GetReferencedAssemblies()

            Dim moduleTypeSymbol2 = comp2.GlobalNamespace.GetMember(Of NamedTypeSymbol)("TypeFromModule")
            Assert.Equal(moduleSymbol2, moduleTypeSymbol2.ContainingModule)
            Assert.Equal(comp2.Assembly, moduleTypeSymbol2.ContainingAssembly)

            Assert.NotEqual(moduleSymbol1, moduleSymbol2)
            Assert.NotEqual(moduleTypeSymbol1, moduleTypeSymbol2)
            AssertEx.Equal(moduleReferences1, moduleReferences2)
        End Sub

        <Fact(), WorkItem(531537, "DevDiv")>
        Public Sub ModuleSymbolReuse_ImplicitType()
            Dim moduleSource =
    <compilation name="lib">
        <file name="lib.vb">
Public Class TypeFromModule
End Class
                    </file>
    </compilation>

            ' Note: we just need *a* module reference for the repro - we're not depending on its contents, name, etc.
            Dim moduleRef = CreateCompilationWithMscorlib(moduleSource, OutputKind.NetModule).EmitToImageReference()

            Dim text1 =
    <compilation name="test">
        <file name="test.vb">
Namespace A
    Sub M()
    End Sub
                    </file>
    </compilation>

            Dim comp1 = CreateCompilationWithMscorlibAndReferences(text1, {moduleRef})
            Dim tree1 = comp1.SyntaxTrees.Single()

            Dim implicitTypeCount1 = comp1.GlobalNamespace.GetMember(Of NamespaceSymbol)("A").GetMembers(TypeSymbol.ImplicitTypeName).Length
            Assert.Equal(1, implicitTypeCount1)


            Dim tree2 = tree1.WithInsertAt(tree1.ToString().Length, "End Namespace")
            Dim comp2 = comp1.ReplaceSyntaxTree(tree1, tree2)

            Dim implicitTypeCount2 = comp2.GlobalNamespace.GetMember(Of NamespaceSymbol)("A").GetMembers(TypeSymbol.ImplicitTypeName).Length
            Assert.Equal(1, implicitTypeCount2)
        End Sub

        <Fact>
        Public Sub CachingAndVisibility()
            Dim cPublic = CreateCompilationWithMscorlib(<code></code>, options:=OptionsDll.WithMetadataImportOptions(MetadataImportOptions.Public))
            Dim cInternal = CreateCompilationWithMscorlib(<code></code>, options:=OptionsDll.WithMetadataImportOptions(MetadataImportOptions.Internal))
            Dim [cAll] = CreateCompilationWithMscorlib(<code></code>, options:=OptionsDll.WithMetadataImportOptions(MetadataImportOptions.All))
            Dim cPublic2 = CreateCompilationWithMscorlib(<code></code>, options:=OptionsDll.WithMetadataImportOptions(MetadataImportOptions.Public))
            Dim cInternal2 = CreateCompilationWithMscorlib(<code></code>, options:=OptionsDll.WithMetadataImportOptions(MetadataImportOptions.Internal))
            Dim cAll2 = CreateCompilationWithMscorlib(<code></code>, options:=OptionsDll.WithMetadataImportOptions(MetadataImportOptions.All))

            Assert.NotSame(cPublic.Assembly.CorLibrary, cInternal.Assembly.CorLibrary)
            Assert.NotSame([cAll].Assembly.CorLibrary, cInternal.Assembly.CorLibrary)
            Assert.NotSame([cAll].Assembly.CorLibrary, cPublic.Assembly.CorLibrary)
            Assert.Same(cPublic.Assembly.CorLibrary, cPublic2.Assembly.CorLibrary)
            Assert.Same(cInternal.Assembly.CorLibrary, cInternal2.Assembly.CorLibrary)
            Assert.Same([cAll].Assembly.CorLibrary, cAll2.Assembly.CorLibrary)
        End Sub

        <Fact>
        Public Sub ImportingPrivateNetModuleMembers()
            Dim moduleSource =
<compilation>
    <file>
Friend Class C

    Private Sub m()
    End Sub
End Class
    </file>
</compilation>

            Dim mainSource = <compilation><file></file></compilation>

            Dim netModule = CreateCompilationWithMscorlib(moduleSource, options:=OptionsNetModule)
            Dim moduleRef = netModule.EmitToImageReference()

            ' All
            Dim mainAll = CreateCompilationWithMscorlibAndReferences(mainSource, {moduleRef}, options:=OptionsDll.WithMetadataImportOptions(MetadataImportOptions.All))
            Dim mAll = mainAll.GlobalNamespace.GetMember(Of NamedTypeSymbol)("C").GetMembers("m")
            Assert.Equal(1, mAll.Length)

            ' Internal
            Dim mainInternal = CreateCompilationWithMscorlibAndReferences(mainSource, {moduleRef}, options:=OptionsDll.WithMetadataImportOptions(MetadataImportOptions.Internal))
            Dim mInternal = mainInternal.GlobalNamespace.GetMember(Of NamedTypeSymbol)("C").GetMembers("m")
            Assert.Equal(0, mInternal.Length)

            ' Public
            Dim mainPublic = CreateCompilationWithMscorlibAndReferences(mainSource, {moduleRef}, options:=OptionsDll.WithMetadataImportOptions(MetadataImportOptions.Public))
            Dim mPublic = mainPublic.GlobalNamespace.GetMember(Of NamedTypeSymbol)("C").GetMembers("m")
            Assert.Equal(0, mPublic.Length)
        End Sub

        <Fact>
        Public Sub ReferenceManager_TestAssemblyMetaData()
            Dim sourceA =
<compilation name="A">
    <file>        
Public Class A
End Class
    </file>
</compilation>

            Dim a = CreateCompilationWithMscorlib(sourceA)

            Dim sourceB =
<compilation name="B">
    <file>        
Public Class B 
    Inherits A
End Class

Public Class Foo
End Class
    </file>
</compilation>

            Dim refa = New MetadataImageReference(a.EmitToArray(), display:="A")
            Dim b = CreateCompilationWithMscorlibAndReferences(sourceB, {refa})
            Dim refmetadata = DirectCast(refa.GetMetadata(), AssemblyMetadata)

            Dim CopyRefMetaData = refmetadata.Copy
            Assert.NotEqual(refmetadata, CopyRefMetaData)
            Assert.Equal(refmetadata.Assembly.ToString, CopyRefMetaData.Assembly.ToString)

            Dim mca1 As Metadata = refa.GetMetadata()
            Dim Copymca1 = mca1.Copy()
            Assert.NotEqual(mca1, Copymca1)
            Assert.Equal(mca1.ToString, Copymca1.ToString)
            Assert.Equal(mca1.Kind, Copymca1.Kind)

            'With no alias this will result in hashcode of 0
            Dim mrp1 As MetadataReferenceProperties = refa.Properties
            Assert.Equal(0, mrp1.GetHashCode)

            'With the use of the alias this will generate a hashcode
            Dim refb = New MetadataImageReference(a.EmitToArray(), display:="A", aliases:=ImmutableArray.Create("Alias1"))
            Dim mrp2 As MetadataReferenceProperties = refb.Properties
            Assert.NotEqual(0, mrp2.GetHashCode)
        End Sub

        <Fact, WorkItem(905495, "DevDiv")>
        Public Sub ReferenceWithNoMetadataSection()
            Dim c = CreateCompilationWithMscorlib({}, {New TestImageReference(TestResources.MetadataTests.Basic.NativeApp, "NativeApp.exe")}, Options.OptionsDll)
            c.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_BadMetaDataReference1).WithArguments("NativeApp.exe", "PE image doesn't contain managed metadata."))
        End Sub

        <Fact, WorkItem(43)>
        Public Sub ReusingCorLibManager()
            Dim corlib1 = VisualBasicCompilation.Create("Comp")
            Dim assembly1 = corlib1.Assembly

            Dim corlib2 = corlib1.Clone()
            Dim assembly2 = corlib2.Assembly

            Assert.Same(assembly1.CorLibrary, assembly1)
            Assert.Same(assembly2.CorLibrary, assembly2)
            Assert.True(corlib1.ReferenceManagerEquals(corlib2))
        End Sub
    End Class
End Namespace
