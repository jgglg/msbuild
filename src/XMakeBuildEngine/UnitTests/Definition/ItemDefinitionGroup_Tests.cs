﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Resources;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Text;
using System.Globalization;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using System.Threading;

using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

namespace Microsoft.Build.UnitTests.Definition
{
    /// <summary>
    /// Class containing tests for the ProjectItemDefinition and related functionality.
    /// </summary>
    [TestClass]
    public class ItemDefinitionGroup_Tests
    {
        /// <summary>
        /// Test for item definition group definitions showing up in project.
        /// </summary>
        [TestMethod]
        public void ItemDefinitionGroupExistsInProject()
        {
            Project p = new Project(XmlReader.Create(new StringReader(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <ItemDefinitionGroup>
                        <Compile>
                            <First>1st</First>
                            <Second>2nd</Second>
                        </Compile>
                    </ItemDefinitionGroup>
	                <Target Name='Build' />
	            </Project>")));

            Assert.IsTrue(ContainsMetadata(p.ItemDefinitions["Compile"].Metadata, "First", "1st"));
            Assert.IsTrue(ContainsMetadata(p.ItemDefinitions["Compile"].Metadata, "Second", "2nd"));
        }

        /// <summary>
        /// Test for multiple item definition group definitions showing up in project.
        /// </summary>
        [TestMethod]
        public void MultipleItemDefinitionGroupExistsInProject()
        {
            Project p = new Project(XmlReader.Create(new StringReader(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <ItemDefinitionGroup>
                        <Compile>
                            <First>1st</First>
                            <Second>2nd</Second>
                        </Compile>
                    </ItemDefinitionGroup>
                    <ItemDefinitionGroup>
                        <Link>
                            <Third>3rd</Third>
                            <Fourth>4th</Fourth>
                        </Link>
                    </ItemDefinitionGroup>
	                <Target Name='Build' />
	            </Project>")));

            Assert.IsTrue(ContainsMetadata(p.ItemDefinitions["Compile"].Metadata, "First", "1st"));
            Assert.IsTrue(ContainsMetadata(p.ItemDefinitions["Compile"].Metadata, "Second", "2nd"));
            Assert.IsTrue(ContainsMetadata(p.ItemDefinitions["Link"].Metadata, "Third", "3rd"));
            Assert.IsTrue(ContainsMetadata(p.ItemDefinitions["Link"].Metadata, "Fourth", "4th"));
        }

        /// <summary>
        /// Tests that items with no metadata inherit from item definition groups
        /// </summary>
        [TestMethod]
        public void EmptyItemsInheritValues()
        {
            Project p = new Project(XmlReader.Create(new StringReader(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <ItemDefinitionGroup>
                        <Compile>
                            <First>1st</First>
                            <Second>2nd</Second>
                        </Compile>
                    </ItemDefinitionGroup>
                    <ItemDefinitionGroup>
                        <Link>
                            <Third>3rd</Third>
                            <Fourth>4th</Fourth>
                        </Link>
                    </ItemDefinitionGroup>
                    <ItemGroup>
                        <Compile Include='a.cs;b.cs' />
                    </ItemGroup>
	                <Target Name='Build' />
	            </Project>")));

            Assert.IsTrue(ItemContainsMetadata(p, "Compile", "a.cs", "First", "1st"));
            Assert.IsTrue(ItemContainsMetadata(p, "Compile", "b.cs", "First", "1st"));
            Assert.IsTrue(ItemContainsMetadata(p, "Compile", "a.cs", "Second", "2nd"));
            Assert.IsTrue(ItemContainsMetadata(p, "Compile", "b.cs", "Second", "2nd"));
            Assert.IsFalse(ItemContainsMetadata(p, "Compile", "a.cs", "Third", "3rd"));
        }

        /// <summary>
        /// Tests that items with metadata override inherited metadata of the same name
        /// </summary>
        [TestMethod]
        public void ItemMetadataOverridesInheritedValues()
        {
            Project p = new Project(XmlReader.Create(new StringReader(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <ItemDefinitionGroup>
                        <Compile>
                            <First>1st</First>
                            <Second>2nd</Second>
                        </Compile>
                    </ItemDefinitionGroup>
                    <ItemDefinitionGroup>
                        <Link>
                            <Third>3rd</Third>
                            <Fourth>4th</Fourth>
                        </Link>
                    </ItemDefinitionGroup>
                    <ItemGroup>
                        <Compile Include='a.cs'>
                            <Foo>Bar</Foo>
                            <First>Not1st</First>
                        </Compile>
                        <Compile Include='b.cs'>
                            <Foo>Bar</Foo>
                        </Compile>
                    </ItemGroup>
                    <ItemGroup>
                        <Link Include='a.o'/>
                    </ItemGroup>
	                <Target Name='Build' />
	            </Project>")));

            Assert.IsTrue(ItemContainsMetadata(p, "Compile", "a.cs", "First", "Not1st"));
            Assert.IsTrue(ItemContainsMetadata(p, "Compile", "a.cs", "Second", "2nd"));
            Assert.IsTrue(ItemContainsMetadata(p, "Compile", "b.cs", "First", "1st"));
            Assert.IsTrue(ItemContainsMetadata(p, "Compile", "b.cs", "Second", "2nd"));
            Assert.IsTrue(ItemContainsMetadata(p, "Link", "a.o", "Third", "3rd"));
            Assert.IsTrue(ItemContainsMetadata(p, "Link", "a.o", "Fourth", "4th"));
        }

        /// <summary>
        /// Tests that item definition doesn't allow item expansion for the conditional.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ItemDefinitionDoesntAllowItemExpansion()
        {
            Project p = new Project(XmlReader.Create(new StringReader(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <ItemGroup>
                        <Compile Include='a.cs'>
                            <Foo>Bar</Foo>
                            <First>Not1st</First>
                        </Compile>
                        <Compile Include='b.cs'>
                            <Foo>Bar</Foo>
                        </Compile>
                    </ItemGroup>
                    <ItemDefinitionGroup>
                        <Compile Condition=""'@(Compile)'!=''"">
                            <First>1st</First>
                            <Second>2nd</Second>
                        </Compile>
                    </ItemDefinitionGroup>
	                <Target Name='Build' />
	            </Project>")));
        }

        /// <summary>
        /// Tests that item definition metadata doesn't allow item expansion for the conditional.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ItemDefinitionMetadataConditionDoesntAllowItemExpansion()
        {
            Project p = new Project(XmlReader.Create(new StringReader(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <ItemGroup>
                        <Compile Include='a.cs'>
                            <Foo>Bar</Foo>
                            <First>Not1st</First>
                        </Compile>
                        <Compile Include='b.cs'>
                            <Foo>Bar</Foo>
                        </Compile>
                    </ItemGroup>
                    <ItemDefinitionGroup>
                        <Compile>
                            <First  Condition=""'@(Compile)'!=''"">1st</First>
                        </Compile>
                    </ItemDefinitionGroup>
	                <Target Name='Build' />
	            </Project>")));
        }

        /// <summary>
        /// Tests that item definition metadata doesn't allow item expansion for the value.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ItemDefinitionMetadataDoesntAllowItemExpansion()
        {
            Project p = new Project(XmlReader.Create(new StringReader(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <ItemGroup>
                        <Compile Include='a.cs'>
                            <Foo>Bar</Foo>
                            <First>Not1st</First>
                        </Compile>
                        <Compile Include='b.cs'>
                            <Foo>Bar</Foo>
                        </Compile>
                    </ItemGroup>
                    <ItemDefinitionGroup>
                        <Compile>
                            <First>@(Compile)</First>
                        </Compile>
                    </ItemDefinitionGroup>
	                <Target Name='Build' />
	            </Project>")));
        }

        /// <summary>
        /// Tests that item metadata which contains a metadata expansion referring to an item type other
        /// than the one this item definition refers to expands to blank.
        /// </summary>
        [TestMethod]
        public void ItemMetadataReferringToDifferentItemGivesEmptyValue()
        {
            Project p = new Project(XmlReader.Create(new StringReader(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <ItemDefinitionGroup>
                        <Compile>
                            <First>1st</First>
                            <Second>2nd</Second>
                        </Compile>
                    </ItemDefinitionGroup>
                    <ItemDefinitionGroup>
                        <Link>
                            <Third>--%(Compile.First)--</Third>
                            <Fourth>4th</Fourth>
                        </Link>
                    </ItemDefinitionGroup>
                    <ItemGroup>
                        <Compile Include='a.cs'>
                            <Foo>Bar</Foo>
                            <First>Not1st</First>
                        </Compile>
                        <Compile Include='b.cs'>
                            <Foo>Bar</Foo>
                        </Compile>
                    </ItemGroup>
                    <ItemGroup>
                        <Link Include='a.o'/>
                    </ItemGroup>
	                <Target Name='Build' />
	            </Project>")));

            Assert.IsTrue(ItemContainsMetadata(p, "Link", "a.o", "Third", "----"));
        }

        /// <summary>
        /// Tests that empty item definition groups are OK.
        /// </summary>
        [TestMethod]
        public void EmptyItemDefinitionGroup()
        {
            Project p = new Project(XmlReader.Create(new StringReader(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <ItemDefinitionGroup>
                    </ItemDefinitionGroup>
                    <ItemGroup>
                        <Compile Include='a.cs;b.cs' />
                    </ItemGroup>
	                <Target Name='Build' />
	            </Project>")));
        }

        /// <summary>
        /// Tests that item definition groups with empty item definitions are OK.
        /// </summary>
        [TestMethod]
        public void EmptyItemDefinitions()
        {
            Project p = new Project(XmlReader.Create(new StringReader(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <ItemDefinitionGroup>
                        <Compile />
                    </ItemDefinitionGroup>
                    <ItemGroup>
                        <Compile Include='a.cs;b.cs'>
                            <Foo>Bar</Foo>
                        </Compile>
                    </ItemGroup>
	                <Target Name='Build' />
	            </Project>")));

            Assert.IsTrue(ItemContainsMetadata(p, "Compile", "a.cs", "Foo", "Bar"));
            Assert.IsTrue(ItemContainsMetadata(p, "Compile", "b.cs", "Foo", "Bar"));
        }

        [TestMethod]
        public void SelfReferencingMetadataReferencesUseItemDefinition()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
   <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>

    <ItemDefinitionGroup>
      <CppCompile>
        <Defines>DEBUG</Defines>
      </CppCompile>
    </ItemDefinitionGroup>

    <ItemGroup>
      <CppCompile Include='a.cpp'>
        <Defines>%(Defines);CODEANALYSIS</Defines>
      </CppCompile>
    </ItemGroup>

    <Target Name='Build'>
      <Message Text='[{@(CppCompile)}{%(CppCompile.Defines)}]' />
    </Target>
  </Project>")));

            p.Build(new string[] { "Build" }, new ILogger[] { logger });
            logger.AssertLogContains("[{a.cpp}{DEBUG;CODEANALYSIS}]"); // Unexpected value after evaluation
        }


        [TestMethod]
        public void SelfReferencingMetadataReferencesUseItemDefinitionInTarget()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
   <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>

    <ItemDefinitionGroup>
      <CppCompile>
        <Defines>DEBUG</Defines>
      </CppCompile>
    </ItemDefinitionGroup>

    <Target Name='Build'>
      <ItemGroup>
        <CppCompile Include='a.cpp'>
          <Defines>%(Defines);CODEANALYSIS</Defines>
        </CppCompile>
      </ItemGroup>

      <Message Text='[{@(CppCompile)}{%(CppCompile.Defines)}]' />
    </Target>
  </Project>")));

            p.Build(new string[] { "Build" }, new ILogger[] { logger });
            logger.AssertLogContains("[{a.cpp}{DEBUG;CODEANALYSIS}]"); // Unexpected value after evaluation
        }

        [TestMethod]
        public void SelfReferencingMetadataReferencesUseItemDefinitionInTargetModify()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
   <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>

    <ItemDefinitionGroup>
      <CppCompile>
        <Defines>DEBUG</Defines>
      </CppCompile>
    </ItemDefinitionGroup>
    <ItemGroup>
      <CppCompile Include='a.cpp' />
    </ItemGroup>

    <Target Name='Build'>
      <ItemGroup>
        <CppCompile>
          <Defines>%(Defines);CODEANALYSIS</Defines>
        </CppCompile>
      </ItemGroup>

      <Message Text='[{@(CppCompile)}{%(CppCompile.Defines)}]' />
    </Target>
  </Project>")));

            p.Build(new string[] { "Build" }, new ILogger[] { logger });
            logger.AssertLogContains("[{a.cpp}{DEBUG;CODEANALYSIS}]"); // Unexpected value after evaluation
        }

        /// <summary>
        /// Tests that item definition groups with false conditions don't produce definitions
        /// </summary>
        [TestMethod]
        public void ItemDefinitionGroupWithFalseCondition()
        {
            Project p = new Project(XmlReader.Create(new StringReader(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <ItemDefinitionGroup Condition=""'$(Foo)'!=''"">
                        <Compile>
                            <First>1st</First>
                        </Compile>
                    </ItemDefinitionGroup>
                    <ItemGroup>
                        <Compile Include='a.cs;b.cs'>
                            <Foo>Bar</Foo>
                        </Compile>
                    </ItemGroup>
	                <Target Name='Build' />
	            </Project>")));

            Assert.IsFalse(p.ItemDefinitions.ContainsKey("Compile"));
            Assert.IsTrue(ItemContainsMetadata(p, "Compile", "a.cs", "Foo", "Bar"));
            Assert.IsFalse(ItemContainsMetadata(p, "Compile", "a.cs", "First", "1st"));
        }

        /// <summary>
        /// Tests that item definition groups with true conditions produce definitions
        /// </summary>
        [TestMethod]
        public void ItemDefinitionGroupWithTrueCondition()
        {
            Project p = new Project(XmlReader.Create(new StringReader(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <ItemDefinitionGroup Condition=""'$(Foo)'==''"">
                        <Compile>
                            <First>1st</First>
                        </Compile>
                    </ItemDefinitionGroup>
                    <ItemGroup>
                        <Compile Include='a.cs;b.cs'>
                            <Foo>Bar</Foo>
                        </Compile>
                    </ItemGroup>
	                <Target Name='Build' />
	            </Project>")));

            Assert.IsTrue(p.ItemDefinitions.ContainsKey("Compile"));
            Assert.IsTrue(ItemContainsMetadata(p, "Compile", "a.cs", "Foo", "Bar"));
            Assert.IsTrue(ItemContainsMetadata(p, "Compile", "a.cs", "First", "1st"));
        }

        /// <summary>
        /// Tests that item definition with false conditions don't produce definitions
        /// </summary>
        [TestMethod]
        public void ItemDefinitionWithFalseCondition()
        {
            Project p = new Project(XmlReader.Create(new StringReader(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <ItemDefinitionGroup>
                        <Compile  Condition=""'$(Foo)'!=''"">
                            <First>1st</First>
                        </Compile>
                    </ItemDefinitionGroup>
                    <ItemGroup>
                        <Compile Include='a.cs;b.cs'>
                            <Foo>Bar</Foo>
                        </Compile>
                    </ItemGroup>
	                <Target Name='Build' />
	            </Project>")));

            Assert.IsFalse(p.ItemDefinitions.ContainsKey("Compile"));
            Assert.IsTrue(ItemContainsMetadata(p, "Compile", "a.cs", "Foo", "Bar"));
            Assert.IsFalse(ItemContainsMetadata(p, "Compile", "a.cs", "First", "1st"));
        }

        /// <summary>
        /// Tests that item definition with true conditions produce definitions
        /// </summary>
        [TestMethod]
        public void ItemDefinitionWithTrueCondition()
        {
            Project p = new Project(XmlReader.Create(new StringReader(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <ItemDefinitionGroup>
                        <Compile Condition=""'$(Foo)'==''"">
                            <First>1st</First>
                        </Compile>
                    </ItemDefinitionGroup>
                    <ItemGroup>
                        <Compile Include='a.cs;b.cs'>
                            <Foo>Bar</Foo>
                        </Compile>
                    </ItemGroup>
	                <Target Name='Build' />
	            </Project>")));

            Assert.IsTrue(p.ItemDefinitions.ContainsKey("Compile"));
            Assert.IsTrue(ItemContainsMetadata(p, "Compile", "a.cs", "Foo", "Bar"));
            Assert.IsTrue(ItemContainsMetadata(p, "Compile", "a.cs", "First", "1st"));
        }

        /// <summary>
        /// Tests that item definition metadata with false conditions don't produce definitions
        /// </summary>
        [TestMethod]
        public void ItemDefinitionMetadataWithFalseCondition()
        {
            Project p = new Project(XmlReader.Create(new StringReader(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <ItemDefinitionGroup>
                        <Compile>
                            <First Condition=""'$(Foo)'!=''"">1st</First>
                        </Compile>
                    </ItemDefinitionGroup>
                    <ItemGroup>
                        <Compile Include='a.cs;b.cs'>
                            <Foo>Bar</Foo>
                        </Compile>
                    </ItemGroup>
	                <Target Name='Build' />
	            </Project>")));

            Assert.IsTrue(p.ItemDefinitions.ContainsKey("Compile"));
            Assert.IsTrue(ItemContainsMetadata(p, "Compile", "a.cs", "Foo", "Bar"));
            Assert.IsFalse(ItemContainsMetadata(p, "Compile", "a.cs", "First", "1st"));
        }

        /// <summary>
        /// Tests that item definition metadata with true conditions produce definitions
        /// </summary>
        [TestMethod]
        public void ItemDefinitionMetadataWithTrueCondition()
        {
            Project p = new Project(XmlReader.Create(new StringReader(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <ItemDefinitionGroup>
                        <Compile>
                            <First Condition=""'$(Foo)'==''"">1st</First>
                        </Compile>
                    </ItemDefinitionGroup>
                    <ItemGroup>
                        <Compile Include='a.cs;b.cs'>
                            <Foo>Bar</Foo>
                        </Compile>
                    </ItemGroup>
	                <Target Name='Build' />
	            </Project>")));

            Assert.IsTrue(p.ItemDefinitions.ContainsKey("Compile"));
            Assert.IsTrue(ItemContainsMetadata(p, "Compile", "a.cs", "Foo", "Bar"));
            Assert.IsTrue(ItemContainsMetadata(p, "Compile", "a.cs", "First", "1st"));
        }

        /// <summary>
        /// Tests that item definition metadata is correctly copied to a destination item
        /// </summary>
        [TestMethod]
        public void ItemDefinitionMetadataCopiedToTaskItem()
        {
            Project p = new Project(XmlReader.Create(new StringReader(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemDefinitionGroup>
                    <ItemA>
                        <MetaA>M-A(b)</MetaA>
                        <MetaB>M-B(b)</MetaB>
                    </ItemA>
                </ItemDefinitionGroup>
            </Project>")));

            Assert.IsTrue(p.ItemDefinitions.ContainsKey("ItemA"));

            ProjectInstance pi = p.CreateProjectInstance();
            ITaskItem noMetaItem = null;
            ITaskItem withMetaItem;

            List<ProjectItemDefinitionInstance> itemdefs = new List<ProjectItemDefinitionInstance>();
            itemdefs.Add(pi.ItemDefinitions["ItemA"]);

            noMetaItem = new TaskItem("NoMetaItem", pi.FullPath);
            withMetaItem = new TaskItem("WithMetaItem", "WithMetaItem", null, itemdefs, ".", false, pi.FullPath);

            // Copy the metadata on the item with no metadata onto the item with metadata
            // from an item definition. The destination item's metadata should be maintained
            noMetaItem.CopyMetadataTo(withMetaItem);

            Assert.AreEqual("M-A(b)", withMetaItem.GetMetadata("MetaA"));
            Assert.AreEqual("M-B(b)", withMetaItem.GetMetadata("MetaB"));
        }

        /// <summary>
        /// Tests that item definition metadata is correctly copied to a destination item
        /// </summary>
        [TestMethod]
        public void ItemDefinitionMetadataCopiedToTaskItem2()
        {
            Project p = new Project(XmlReader.Create(new StringReader(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemDefinitionGroup>
                    <ItemA>
                        <MetaA>M-A(b)</MetaA>
                        <MetaB>M-B(b)</MetaB>
                    </ItemA>
                </ItemDefinitionGroup>
            </Project>")));

            Assert.IsTrue(p.ItemDefinitions.ContainsKey("ItemA"));

            ProjectInstance pi = p.CreateProjectInstance();
            ITaskItem noMetaItem = null;
            ITaskItem withMetaItem;

            List<ProjectItemDefinitionInstance> itemdefs = new List<ProjectItemDefinitionInstance>();
            itemdefs.Add(pi.ItemDefinitions["ItemA"]);

            noMetaItem = new TaskItem("NoMetaItem", pi.FullPath);
            noMetaItem.SetMetadata("MetaA", "NEWMETA_A");

            withMetaItem = new TaskItem("WithMetaItem", "WithMetaItem", null, itemdefs, ".", false, pi.FullPath);

            // Copy the metadata on the item with no metadata onto the item with metadata
            // from an item definition. The destination item's metadata should be maintained
            noMetaItem.CopyMetadataTo(withMetaItem);

            // New direct metadata takes precedence over item definitions on the destination item
            Assert.AreEqual("NEWMETA_A", withMetaItem.GetMetadata("MetaA"));
            Assert.AreEqual("M-B(b)", withMetaItem.GetMetadata("MetaB"));
        }

        /// <summary>
        /// Tests that item definition metadata is correctly copied to a destination item
        /// </summary>
        [TestMethod]
        public void ItemDefinitionMetadataCopiedToTaskItem3()
        {
            Project p = new Project(XmlReader.Create(new StringReader(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemDefinitionGroup>
                    <ItemA>
                        <MetaA>M-A(b)</MetaA>
                        <MetaB>M-B(b)</MetaB>
                    </ItemA>
                </ItemDefinitionGroup>
                <ItemGroup>
                    <ItemA Include='SomeItemA' />
                </ItemGroup>
            </Project>")));

            Assert.IsTrue(p.ItemDefinitions.ContainsKey("ItemA"));

            ProjectInstance pi = p.CreateProjectInstance();
            ITaskItem noMetaItem = null;
            ITaskItem withMetaItem = null;

            List<ProjectItemDefinitionInstance> itemdefs = new List<ProjectItemDefinitionInstance>();
            itemdefs.Add(pi.ItemDefinitions["ItemA"]);

            noMetaItem = new TaskItem("NoMetaItem", pi.FullPath);

            // No the ideal way to get the first item, but there is no other way since GetItems returns an IEnumerable :(
            foreach (ProjectItemInstance item in pi.GetItems("ItemA"))
            {
                withMetaItem = item;
            }

            // Copy the metadata on the item with no metadata onto the item with metadata
            // from an item definition. The destination item's metadata should be maintained
            noMetaItem.CopyMetadataTo(withMetaItem);

            Assert.AreEqual("M-A(b)", withMetaItem.GetMetadata("MetaA"));
            Assert.AreEqual("M-B(b)", withMetaItem.GetMetadata("MetaB"));
        }

        #region Project tests

        [TestMethod]
        public void BasicItemDefinitionInProject()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemGroup>
                    <CppCompile Include='a.cpp'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <CppCompile>
                      <Defines>DEBUG</Defines>
                    </CppCompile>
                  </ItemDefinitionGroup> 
                  <ItemGroup>
                    <CppCompile Include='b.cpp'/>
                  </ItemGroup>
                  <Target Name=""t"">
                    <Message Text=""[%(CppCompile.Identity)==%(CppCompile.Defines)]""/>
                  </Target>
                </Project>
            ")));
            p.Build("t", new ILogger[] { logger });

            logger.AssertLogContains("[a.cpp==DEBUG]", "[b.cpp==DEBUG]");
        }

        [TestMethod]
        public void EscapingInItemDefinitionInProject()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemGroup>
                    <i Include='i1'/>
                  </ItemGroup>
                  <ItemDefinitionGroup Condition=""'%24'=='$'"">
                    <i Condition=""'%24'=='$'"">
                      <m Condition=""'%24'=='$'"">%24(xyz)</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <Target Name=""t"">
                    <Message Text=""[%(i.m)]""/>
                  </Target>
                </Project>
            ")));
            p.Build("t", new ILogger[] { logger });

            logger.AssertLogContains("[$(xyz)]");
        }


        [TestMethod]
        public void ItemDefinitionForOtherItemType()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemGroup>
                    <i Include='i1'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <j>
                      <m>m1</m>
                    </j>
                  </ItemDefinitionGroup> 
                  <Target Name=""t"">
                    <Message Text=""[%(i.m)]""/>
                  </Target>
                </Project>
            ")));
            p.Build("t", new ILogger[] { logger });

            logger.AssertLogContains("[]");
        }

        [TestMethod]
        public void RedefinitionLastOneWins()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemGroup>
                    <i Include='i1'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                      <n>n1</n>
                    </i>
                  </ItemDefinitionGroup> 
                  <ItemDefinitionGroup>
                    <i>
                      <m>m2</m>
                      <o>o1</o>
                    </i>
                  </ItemDefinitionGroup> 
                  <Target Name=""t"">
                    <Message Text=""[%(i.m)-%(i.n)-%(i.o)]""/>
                  </Target>
                </Project>
            ")));
            p.Build("t", new ILogger[] { logger });

            logger.AssertLogContains("[m2-n1-o1]");
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ItemExpressionInDefaultMetadataValueErrors()
        {
            // We don't allow item expressions on an ItemDefinitionGroup because there are no items when IDG is evaluated.
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemDefinitionGroup>
                    <i>
                      <m>@(x)</m>
                    </i>
                  </ItemDefinitionGroup> 
                </Project>
            ")));
            p.Build("t", new ILogger[] { logger });
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void UnqualifiedMetadataConditionOnItemDefinitionGroupErrors()
        {
            // We don't allow unqualified metadata on an ItemDefinitionGroup because we don't know what item type it refers to.
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemDefinitionGroup Condition=""'%(m)'=='m1'""/>
                </Project>
            ")));
            p.Build("t", new ILogger[] { logger });
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void QualifiedMetadataConditionOnItemDefinitionGroupErrors()
        {
            // We don't allow qualified metadata because it's not worth distinguishing from unqualified, when you can just move the condition to the child.
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemDefinitionGroup Condition=""'%(x.m)'=='m1'""/>
                </Project>
            ")));
            p.Build("t", new ILogger[] { logger });
        }

        [TestMethod]
        public void MetadataConditionOnItemDefinition()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemGroup>
                    <i Include='i1'/>
                    <j Include='j1'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                    </i>
                    <j>
                      <n>n1</n>
                    </j>
                  </ItemDefinitionGroup> 
                  <ItemDefinitionGroup>
                    <i Condition=""'%(m)'=='m1'"">
                      <m>m2</m>
                    </i>
                    <!-- verify j metadata is distinct -->
                    <j Condition=""'%(j.n)'=='n1' and '%(n)'=='n1'"">
                      <n>n2</n>   
                    </j>
                  </ItemDefinitionGroup> 
                  <Target Name=""t"">
                    <Message Text=""[%(i.m)]""/>
                    <Message Text=""[%(j.n)]""/>
                  </Target>
                </Project>
            ")));
            p.Build("t", new ILogger[] { logger });

            logger.AssertLogContains("[m2]", "[n2]");
        }

        [TestMethod]
        public void QualifiedMetadataConditionOnItemDefinitionBothQualifiedAndUnqualified()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemGroup>
                    <i Include='i1'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <ItemDefinitionGroup>
                    <i Condition=""'%(i.m)'=='m1' and '%(m)'=='m1'"">
                      <m>m2</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <Target Name=""t"">
                    <Message Text=""[%(i.m)]""/>
                  </Target>
                </Project>
            ")));
            p.Build("t", new ILogger[] { logger });

            logger.AssertLogContains("[m2]");
        }

        [TestMethod]
        public void FalseMetadataConditionOnItemDefinitionBothQualifiedAndUnqualified()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemGroup>
                    <i Include='i1'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <ItemDefinitionGroup>
                    <i Condition=""'%(m)'=='m2' or '%(i.m)'!='m1'"">
                      <m>m3</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <Target Name=""t"">
                    <Message Text=""[%(i.m)]""/>
                  </Target>
                </Project>
            ")));
            p.Build("t", new ILogger[] { logger });

            logger.AssertLogContains("[m1]");
        }

        [TestMethod]
        public void MetadataConditionOnItemDefinitionChildBothQualifiedAndUnqualified()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemGroup>
                    <i Include='i1'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                      <n>n1</n>
                    </i>
                  </ItemDefinitionGroup> 
                  <ItemDefinitionGroup>
                    <i>
                      <m Condition=""'%(m)'=='m1' and '%(n)'=='n1' and '%(i.m)'=='m1'"">m2</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <Target Name=""t"">
                    <Message Text=""[%(i.m)]""/>
                  </Target>
                </Project>
            ")));
            p.Build("t", new ILogger[] { logger });

            logger.AssertLogContains("[m2]");
        }

        [TestMethod]
        public void FalseMetadataConditionOnItemDefinitionChildBothQualifiedAndUnqualified()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemGroup>
                    <i Include='i1'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                      <n>n1</n>
                    </i>
                  </ItemDefinitionGroup> 
                  <ItemDefinitionGroup>
                    <i>
                      <m Condition=""'%(m)'=='m2' or !('%(n)'=='n1') or '%(i.m)' != 'm1'"">m3</m>
                    </i>
                  </ItemDefinitionGroup>
                  <Target Name=""t"">
                    <Message Text=""[%(i.m)]""/>
                  </Target>
                </Project>
            ")));
            p.Build("t", new ILogger[] { logger });

            logger.AssertLogContains("[m1]");
        }

        [TestMethod]
        public void MetadataConditionOnItemDefinitionAndChildQualifiedWithUnrelatedItemType()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemGroup>
                    <i Include='i1'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <ItemDefinitionGroup>
                    <i Condition=""'%(j.m)'=='' and '%(j.m)'!='x'"">
                      <m Condition=""'%(j.m)'=='' and '%(j.m)'!='x'"">m2</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <Target Name=""t"">
                    <Message Text=""[%(i.m)]""/>
                  </Target>
                </Project>
            ")));
            p.Build("t", new ILogger[] { logger });

            logger.AssertLogContains("[m2]");
        }

        /// <summary>
        /// Make ItemDefinitionGroup inside a target produce a nice error.
        /// It will normally produce an error due to the invalid child tag, but 
        /// we want to error even if there's no child tag. This will make it 
        /// easier to support it inside targets in a future version.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ItemDefinitionInTargetErrors()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <Target Name=""t"">
                    <ItemDefinitionGroup/>
                  </Target>
                </Project>
            ")));
            bool result = p.Build("t", new ILogger[] { logger });
        }

        // Verify that anyone with a task named "ItemDefinitionGroup" can still
        // use it by fully qualifying the name.
        [TestMethod]
        public void ItemDefinitionGroupTask()
        {
            MockLogger ml = Helpers.BuildProjectWithNewOMExpectSuccess(String.Format(@"
                    <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                        <UsingTask TaskName=""ItemDefinitionGroup"" AssemblyFile=""{0}""/>
                        <Target Name=""Build"">
                            <Microsoft.Build.UnitTests.Definition.ItemDefinitionGroup/>
                        </Target>
                    </Project>
               ", new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath));

            Assert.IsTrue(ml.FullLog.Contains("In ItemDefinitionGroup task."));
        }

        [TestMethod]
        public void MetadataOnItemWins()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemGroup>
                    <CppCompile Include='a.cpp'>
                      <Defines>RETAIL</Defines>
                    </CppCompile>
                    <CppCompile Include='b.cpp'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <CppCompile>
                      <Defines>DEBUG</Defines>
                    </CppCompile>
                  </ItemDefinitionGroup> 
                  <Target Name=""t"">
                    <Message Text=""[%(CppCompile.Identity)==%(CppCompile.Defines)]""/>
                  </Target>
                </Project>
            ")));
            p.Build("t", new ILogger[] { logger });

            logger.AssertLogContains("[a.cpp==RETAIL]", "[b.cpp==DEBUG]");
        }

        [TestMethod]
        public void MixtureOfItemAndDefaultMetadata()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemGroup>
                    <CppCompile Include='a.cpp'>
                      <WarningLevel>4</WarningLevel>
                    </CppCompile>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <CppCompile>
                      <Defines>DEBUG</Defines>
                    </CppCompile>
                  </ItemDefinitionGroup> 
                  <Target Name=""t"">
                    <Message Text=""[%(CppCompile.Identity)==%(CppCompile.Defines)]""/>
                    <Message Text=""[%(CppCompile.Identity)==%(CppCompile.WarningLevel)]""/>
                  </Target>
                </Project>
            ")));
            p.Build("t", new ILogger[] { logger });

            logger.AssertLogContains("[a.cpp==DEBUG]", "[a.cpp==4]");
        }

        [TestMethod]
        public void IntrinsicTaskModifyingDefaultMetadata()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemGroup>
                    <i Include='i1'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <Target Name=""t"">
                    <ItemGroup>
                      <i>
                        <m>m2</m>
                      </i>
                    </ItemGroup>
                    <Message Text=""[%(i.m)]""/>
                  </Target>
                </Project>
            ")));
            p.Build("t", new ILogger[] { logger });

            logger.AssertLogContains("[m2]");
        }

        [TestMethod]
        public void IntrinsicTaskConsumingDefaultMetadata()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemGroup>
                    <i Include='i1'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <Target Name=""t"">
                    <ItemGroup>
                      <i Condition=""'%(i.m)'=='m1'"">
                        <n Condition=""'%(m)'=='m1'"">n2</n>
                      </i>
                    </ItemGroup>
                    <Message Text=""[%(i.n)]""/>
                  </Target>
                </Project>
            ")));
            p.Build("t", new ILogger[] { logger });

            logger.AssertLogContains("[n2]");
        }

        [TestMethod]
        public void DefinitionInImportedFile()
        {
            MockLogger logger = new MockLogger();
            string importedFile = null;

            try
            {
                importedFile = FileUtilities.GetTemporaryFile();
                File.WriteAllText(importedFile, @"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                  <ItemDefinitionGroup>
                    <CppCompile>
                      <Defines>DEBUG</Defines>
                    </CppCompile>
                  </ItemDefinitionGroup> 
                </Project>
            ");
                Project p = new Project(XmlReader.Create(new StringReader(@"
                    <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                      <ItemGroup>
                        <CppCompile Include='a.cpp'/>                      
                      </ItemGroup>
                      <Import Project='" + importedFile + @"'/>
                      <Target Name=""t"">
                        <Message Text=""[%(CppCompile.Identity)==%(CppCompile.Defines)]""/>
                      </Target>
                    </Project>
                ")));
                p.Build("t", new ILogger[] { logger });

                logger.AssertLogContains("[a.cpp==DEBUG]");
            }
            finally
            {
                ObjectModelHelpers.DeleteTempFiles(new string[] { importedFile });
            }
        }

        /// <summary>
        /// Item added to project should pick up the item
        /// definitions that project has.
        [TestMethod]
        public void ProjectAddNewItemPicksUpProjectItemDefinitions()
        {
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                    </i>
                  </ItemDefinitionGroup> 
                </Project>
                ")));

            p.AddItem("i", "i1");
            p.ReevaluateIfNecessary();

            Assert.IsTrue(ItemContainsMetadata(p, "i", "i1", "m", "m1"));
        }

        /// <summary>
        /// Item added to project should pick up the item
        /// definitions that project has.
        [TestMethod]
        public void ProjectAddNewItemExistingGroupPicksUpProjectItemDefinitions()
        {
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <ItemGroup>
                    <i Include='i2'>
                      <m>m2</m>
                    </i>
                  </ItemGroup>
                </Project>
                ")));

            p.AddItem("i", "i1");
            p.ReevaluateIfNecessary();

            Assert.IsTrue(ItemContainsMetadata(p, "i", "i1", "m", "m1"));
            Assert.IsTrue(ItemContainsMetadata(p, "i", "i2", "m", "m2"));
        }

        [TestMethod]
        public void ItemsEmittedByTaskPickUpItemDefinitions()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                      <n>n1</n>
                    </i>
                  </ItemDefinitionGroup> 
                  <Target Name=""t"">
                    <CreateItem Include=""i1"" AdditionalMetadata=""n=n2"">
                      <Output ItemName=""i"" TaskParameter=""Include""/>
                    </CreateItem>
                    <Message Text=""[%(i.m)][%(i.n)]""/>
                  </Target>
                </Project>
            ")));

            p.Build("t", new ILogger[] { logger });

            logger.AssertLogContains("[m1][n2]");
        }

        [TestMethod]
        public void ItemsEmittedByIntrinsicTaskPickUpItemDefinitions()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                      <n>n1</n>
                    </i>
                  </ItemDefinitionGroup> 
                  <Target Name=""t"">
                    <ItemGroup>
                      <i Include=""i1"">
                        <n>n2</n>
                      </i>
                    </ItemGroup>
                    <Message Text=""[%(i.m)][%(i.n)]""/>
                  </Target>
                </Project>
            ")));

            p.Build("t", new ILogger[] { logger });

            logger.AssertLogContains("[m1][n2]");
        }

        /// <summary>
        /// When items are passed with an item list expression, default metadata values on the source
        /// items should become regular metadata values on the new items, unless overridden.
        /// </summary>
        [TestMethod]
        public void ItemsEmittedByIntrinsicTaskConsumingItemExpression_SourceDefaultMetadataPassed()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <ItemGroup>
                    <i Include=""i1""/>
                  </ItemGroup>
                  <Target Name=""t"">
                    <ItemGroup>
                      <j Include=""@(i)""/>
                    </ItemGroup>
                    <Message Text=""[%(j.m)]""/>
                  </Target>
                </Project>
            ")));

            p.Build("t", new ILogger[] { logger });

            logger.AssertLogContains("[m1]");
        }

        /// <summary>
        /// Default metadata on the source item list is overridden by matching metadata explicitly on the destination
        /// </summary>
        [TestMethod]
        public void ItemsEmittedByIntrinsicTaskConsumingItemExpression_DestinationExplicitMetadataBeatsSourceDefaultMetadata()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <ItemGroup>
                    <i Include=""i1""/>
                  </ItemGroup>
                  <Target Name=""t"">
                    <ItemGroup>
                      <j Include=""@(i)"">
                        <m>m2</m>
                      </j>
                    </ItemGroup>
                    <Message Text=""[%(j.m)]""/>
                  </Target>
                </Project>
            ")));

            p.Build("t", new ILogger[] { logger });

            logger.AssertLogContains("[m2]");
        }

        /// <summary>
        /// When items of type X are copied into a list of type Y, default metadata applicable to type X override
        /// any matching default metadata applicable to type Y.
        /// </summary>
        /// <remarks>
        /// Either behavior here is fairly reasonable. We decided on this way around based on feedback from VC.
        /// Note: this differs from how Orcas did it.
        /// </remarks>
        [TestMethod]
        public void ItemsEmittedByIntrinsicTaskConsumingItemExpression_DestinationDefaultMetadataOverriddenBySourceDefaultMetadata()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                    </i>
                    <j>
                      <m>m2</m>
                    </j>
                  </ItemDefinitionGroup> 
                  <ItemGroup>
                    <i Include=""n1""/>
                    <j Include=""@(i)""/>
                  </ItemGroup>
                  <Target Name=""t"">
                    <ItemGroup>
                      <i Include=""n2""/>
                      <j Include=""@(i)""/>
                    </ItemGroup>
                    <Message Text=""[%(j.m)]""/>
                  </Target>
                </Project>
            ")));

            Assert.AreEqual("m1", p.GetItems("j").First().GetMetadataValue("m"));

            p.Build("t", new ILogger[] { logger });

            logger.AssertLogContains("[m1]");
            logger.AssertLogDoesntContain("[m2]");
        }

        /// <summary>
        /// Default and explicit metadata on both source and destination.
        /// Item definition metadata from the source override item definition on the destination.
        /// </summary>
        [TestMethod]
        public void ItemsEmittedByIntrinsicTaskConsumingItemExpression_Combination_OutsideTarget()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemDefinitionGroup>
                    <i>
                      <m>im1</m>
                      <n>in1</n>
                      <o>io1</o>
                      <p>ip1</p>
                    </i>
                    <j>
                      <m>jm3</m>
                      <n>jn3</n>
                      <q>jq3</q>
                    </j>
                    <k>
                      <m>km4</m>
                      <q>kq4</q>
                      <r>kr4</r>
                    </k>
                  </ItemDefinitionGroup> 
                  <ItemGroup>
                    <i Include=""1"">
                      <o>io2</o>
                      <s>is2</s>
                    </i>
                    <j Include=""2""/>
                    <k Include=""3"">
                      <M>km5</M>
                    </k>
                  </ItemGroup>
                  <ItemGroup>
                    <j Include=""@(i)"">
                      <m>jm6</m>
                    </j>
                    <k Include=""@(j)"">
                      <s>ks3</s>
                    </k>
                  </ItemGroup>
                </Project>
            ")));

            Assert.AreEqual("im1", p.GetItems("i").First().GetMetadataValue("m"));
            Assert.AreEqual("in1", p.GetItems("i").First().GetMetadataValue("n"));
            Assert.AreEqual("io2", p.GetItems("i").First().GetMetadataValue("o"));
            Assert.AreEqual("ip1", p.GetItems("i").First().GetMetadataValue("p"));
            Assert.AreEqual("", p.GetItems("i").First().GetMetadataValue("q"));

            Assert.AreEqual("jm3", p.GetItems("j").First().GetMetadataValue("m"));
            Assert.AreEqual("jn3", p.GetItems("j").First().GetMetadataValue("n"));
            Assert.AreEqual("", p.GetItems("j").First().GetMetadataValue("o"));
            Assert.AreEqual("", p.GetItems("j").First().GetMetadataValue("p"));
            Assert.AreEqual("jq3", p.GetItems("j").First().GetMetadataValue("q"));

            Assert.AreEqual("jm6", p.GetItems("j").ElementAt(1).GetMetadataValue("m"));
            Assert.AreEqual("in1", p.GetItems("j").ElementAt(1).GetMetadataValue("n"));
            Assert.AreEqual("io2", p.GetItems("j").ElementAt(1).GetMetadataValue("o"));
            Assert.AreEqual("ip1", p.GetItems("j").ElementAt(1).GetMetadataValue("p"));
            Assert.AreEqual("jq3", p.GetItems("j").ElementAt(1).GetMetadataValue("q"));

            Assert.AreEqual("km5", p.GetItems("k").ElementAt(0).GetMetadataValue("m"));
            Assert.AreEqual("", p.GetItems("k").ElementAt(0).GetMetadataValue("n"));
            Assert.AreEqual("", p.GetItems("k").ElementAt(0).GetMetadataValue("o"));
            Assert.AreEqual("", p.GetItems("k").ElementAt(0).GetMetadataValue("p"));
            Assert.AreEqual("kq4", p.GetItems("k").ElementAt(0).GetMetadataValue("q"));
            Assert.AreEqual("kr4", p.GetItems("k").ElementAt(0).GetMetadataValue("r"));
            Assert.AreEqual("", p.GetItems("k").ElementAt(0).GetMetadataValue("s"));

            Assert.AreEqual("jm3", p.GetItems("k").ElementAt(1).GetMetadataValue("m"));
            Assert.AreEqual("jn3", p.GetItems("k").ElementAt(1).GetMetadataValue("n"));
            Assert.AreEqual("", p.GetItems("k").ElementAt(1).GetMetadataValue("o"));
            Assert.AreEqual("", p.GetItems("k").ElementAt(1).GetMetadataValue("p"));
            Assert.AreEqual("jq3", p.GetItems("k").ElementAt(1).GetMetadataValue("q"));
            Assert.AreEqual("kr4", p.GetItems("k").ElementAt(1).GetMetadataValue("r"));
            Assert.AreEqual("ks3", p.GetItems("k").ElementAt(1).GetMetadataValue("s"));

            Assert.AreEqual("jm6", p.GetItems("k").ElementAt(2).GetMetadataValue("m"));
            Assert.AreEqual("in1", p.GetItems("k").ElementAt(2).GetMetadataValue("n"));
            Assert.AreEqual("io2", p.GetItems("k").ElementAt(2).GetMetadataValue("o"));
            Assert.AreEqual("ip1", p.GetItems("k").ElementAt(2).GetMetadataValue("p"));
            Assert.AreEqual("jq3", p.GetItems("k").ElementAt(2).GetMetadataValue("q"));
            Assert.AreEqual("kr4", p.GetItems("k").ElementAt(2).GetMetadataValue("r"));
            Assert.AreEqual("ks3", p.GetItems("k").ElementAt(1).GetMetadataValue("s"));
        }

        /// <summary>
        /// Default and explicit metadata on both source and destination.
        /// Item definition metadata from the source override item definition on the destination.
        /// </summary>
        [TestMethod]
        public void ItemsEmittedByIntrinsicTaskConsumingItemExpression_Combination_InsideTarget()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemDefinitionGroup>
                    <i>
                      <m>im1</m>
                      <n>in1</n>
                      <o>io1</o>
                      <p>ip1</p>
                    </i>
                    <j>
                      <m>jm3</m>
                      <n>jn3</n>
                      <q>jq3</q>
                    </j>
                    <k>
                      <m>km4</m>
                      <q>kq4</q>
                    </k>
                  </ItemDefinitionGroup> 
                  <ItemGroup>
                    <i Include=""1"">
                      <o>io2</o>
                    </i>
                    <j Include=""2""/>
                    <k Include=""3"">
                      <M>km5</M>
                    </k>
                  </ItemGroup>
                  <Target Name=""t"">
                    <ItemGroup>
                      <j Include=""@(i)"">
                        <m>jm6</m>
                      </j>
                      <k Include=""@(j)""/>
                    </ItemGroup>
                    <Message Text=""i:%(identity) [%(i.m)][%(i.n)][%(i.o)][%(i.p)][%(i.q)]""/>
                    <Message Text=""j:%(identity) [%(j.m)][%(j.n)][%(j.o)][%(j.p)][%(j.q)]""/>
                    <Message Text=""k:%(identity) [%(k.m)][%(k.n)][%(k.o)][%(k.p)][%(k.q)]""/>
                  </Target>
                </Project>
            ")));

            p.Build("t", new ILogger[] { logger });

            logger.AssertLogContains("i:1 [im1][in1][io2][ip1][]");
            logger.AssertLogContains("j:2 [jm3][jn3][][][jq3]");
            logger.AssertLogContains("j:1 [jm6][in1][io2][ip1][jq3]");
            logger.AssertLogContains("k:3 [km5][][][][kq4]");
            logger.AssertLogContains("k:2 [jm3][jn3][][][jq3]");
            logger.AssertLogContains("k:1 [jm6][in1][io2][ip1][jq3]");
        }

        [TestMethod]
        public void MutualReferenceToDefinition1()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                      <n>~%(m)~</n>
                    </i>
                  </ItemDefinitionGroup> 
                    <ItemGroup>
                      <i Include=""i1""/>
                    </ItemGroup>   
                  <Target Name=""t"">
                    <Message Text=""[%(i.m)][%(i.n)]""/>
                  </Target>
                </Project>
            ")));

            p.Build("t", new ILogger[] { logger });

            logger.AssertLogContains("[m1][~m1~]");
        }

        [TestMethod]
        public void MutualReferenceToDefinition2()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemDefinitionGroup>
                    <i>
                      <m>~%(n)~</m>
                      <n>n1</n>
                    </i>
                  </ItemDefinitionGroup> 
                    <ItemGroup>
                      <i Include=""i1""/>
                    </ItemGroup>   
                  <Target Name=""t"">
                    <Message Text=""[%(i.m)][%(i.n)]""/>
                  </Target>
                </Project>
            ")));

            p.Build("t", new ILogger[] { logger });

            logger.AssertLogContains("[~~][n1]");
        }

        [TestMethod]
        public void MutualReferenceToDefinition3()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                      <n>%(i.m)</n>
                      <o>%(j.m)</o>
                    </i>
                  </ItemDefinitionGroup> 
                    <ItemGroup>
                      <i Include=""i1""/>
                    </ItemGroup>   
                  <Target Name=""t"">
                    <Message Text=""[%(i.m)][%(i.n)][%(i.o)]""/>
                  </Target>
                </Project>
            ")));

            p.Build("t", new ILogger[] { logger });

            logger.AssertLogContains("[m1][m1][]");
        }

        [TestMethod]
        public void ProjectReevaluationReevaluatesItemDefinitions()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <PropertyGroup>
                    <Defines>CODEANALYSIS</Defines>
                  </PropertyGroup>
                  <ItemGroup>
                    <CppCompile Include='a.cpp'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <CppCompile>
                      <Defines Condition=""'$(BuildFlavor)'=='ret'"">$(Defines);RETAIL</Defines>
                      <Defines Condition=""'$(BuildFlavor)'=='chk'"">$(Defines);DEBUG</Defines>
                    </CppCompile>
                  </ItemDefinitionGroup> 
                  <Target Name=""t"">
                    <Message Text=""[%(CppCompile.Identity)==%(CppCompile.Defines)]""/>
                  </Target>
                </Project>
            ")));

            p.SetProperty("BuildFlavor", "ret");

            p.Build("t", new ILogger[] { logger });

            logger.AssertLogContains("[a.cpp==CODEANALYSIS;RETAIL]");

            Assert.IsTrue(ItemContainsMetadata(p, "CppCompile", "a.cpp", "Defines", "CODEANALYSIS;RETAIL"));

            p.SetProperty("BuildFlavor", "chk");
            p.ReevaluateIfNecessary();

            Assert.IsTrue(ItemContainsMetadata(p, "CppCompile", "a.cpp", "Defines", "CODEANALYSIS;DEBUG"));
        }

        [TestMethod]
        [Ignore]
        // Ignore: Changes to the current directory interfere with the toolset reader.
        public void MSBuildCallDoesNotAffectCallingProjectsDefinitions()
        {
            string otherProject = null;

            try
            {
                otherProject = FileUtilities.GetTemporaryFile();
                string otherProjectContent = @"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemGroup>
                    <i Include='i1'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m2</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <Target Name=""t"">
                    <Message Text=""[CHILD:%(i.m)]""/>
                  </Target>
                </Project>";

                using (StreamWriter writer = new StreamWriter(otherProject))
                {
                    writer.Write(otherProjectContent);
                }

                MockLogger logger = new MockLogger();
                Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemGroup>
                    <i Include='i1'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <Target Name=""t"">
                    <Message Text=""[PARENT-before:%(i.m)]""/>
                    <MSBuild Projects=""" + otherProject + @"""/>
                    <Message Text=""[PARENT-after:%(i.m)]""/>
                  </Target>
                </Project>
            ")));

                p.Build("t", new ILogger[] { logger });

                logger.AssertLogContains("[PARENT-before:m1]", "[CHILD:m2]", "[PARENT-after:m1]");
            }
            finally
            {
                File.Delete(otherProject);
            }
        }

        [TestMethod]
        [Ignore]
        // Ignore: Changes to the current directory interfere with the toolset reader.
        public void DefaultMetadataTravelWithTargetOutputs()
        {
            string otherProject = null;

            try
            {
                otherProject = FileUtilities.GetTemporaryFile();
                string otherProjectContent = @"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemGroup>
                    <i Include='i1'>
                       <m>m1</m>
                    </i>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <i>
                      <n>n1</n>
                    </i>
                  </ItemDefinitionGroup> 
                  <Target Name=""t"" Outputs=""@(i)"">
                    <Message Text=""[CHILD:%(i.Identity):m=%(i.m),n=%(i.n)]""/>
                  </Target>
                </Project>";

                using (StreamWriter writer = new StreamWriter(otherProject))
                {
                    writer.Write(otherProjectContent);
                }

                MockLogger logger = new MockLogger();
                Project p = new Project(XmlReader.Create(new StringReader(@"
                <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <Target Name=""t"">
                    <MSBuild Projects=""" + otherProject + @""">
                       <Output TaskParameter='TargetOutputs' ItemName='i'/>
                    </MSBuild>
                    <Message Text=""[PARENT:%(i.Identity):m=%(i.m),n=%(i.n)]""/>
                  </Target>
                </Project>
            ")));

                p.Build("t", new ILogger[] { logger });

                logger.AssertLogContains("[CHILD:i1:m=m1,n=n1]", "[PARENT:i1:m=m1,n=n1]");
            }
            finally
            {
                File.Delete(otherProject);
            }
        }

        #endregion
        /// <summary>
        /// Determines if the specified item contains the specified metadata
        /// </summary>
        /// <param name="project">The project.</param>
        /// <param name="itemType">The item type.</param>
        /// <param name="itemInclude">The item include.</param>
        /// <param name="name">The metadata name.</param>
        /// <param name="value">The metadata value.</param>
        /// <returns>True if the item contains the metadata, false otherwise.</returns>
        private bool ItemContainsMetadata(Project project, string itemType, string itemInclude, string name, string value)
        {
            foreach (ProjectItem item in project.GetItems(itemType))
            {
                if (item.EvaluatedInclude == itemInclude)
                {
                    return ContainsMetadata(item.Metadata, name, value);
                }
            }

            return false;
        }

        /// <summary>
        /// Determines if the metadata collection contains the named metadata with the specified value
        /// </summary>
        /// <param name="metadata">The collection.</param>
        /// <param name="name">The metadata name.</param>
        /// <param name="value">The metadata value.</param>
        /// <returns>True if the collection contains the metadata, false otherwise.</returns>
        private bool ContainsMetadata(IEnumerable<ProjectMetadata> metadata, string name, string value)
        {
            foreach (ProjectMetadata metadataEntry in metadata)
            {
                if (metadataEntry.Name == name && metadataEntry.EvaluatedValue == value)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public class ItemDefinitionGroup : Microsoft.Build.Utilities.Task
    {
        public override bool Execute()
        {
            Log.LogMessage("In ItemDefinitionGroup task.");
            return true;
        }
    }
}
