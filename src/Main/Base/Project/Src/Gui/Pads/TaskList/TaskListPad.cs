﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using System.Text;
using System.Windows;
using System.Windows.Controls;
using ICSharpCode.Core.Presentation;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.SharpDevelop.Editor;
using ICSharpCode.SharpDevelop.Project;
using ICSharpCode.SharpDevelop.WinForms;
using ICSharpCode.SharpDevelop.Workbench;

namespace ICSharpCode.SharpDevelop.Gui
{
	public class TaskListPad : AbstractPadContent, IClipboardHandler
	{
		public const string DefaultContextMenuAddInTreeEntry = "/SharpDevelop/Pads/TaskList/TaskContextMenu";
		
		static TaskListPad instance;
		Dictionary<string, bool> displayedTokens;
		readonly ObservableCollection<SDTask> tasks;
		IUnresolvedTypeDefinition oldClass;
		int selectedScopeIndex = 0;
		bool isInitialized = false;
		
		public bool IsInitialized {
			get { return isInitialized; }
		}
		
		ToolBar toolBar;
		DockPanel contentPanel = new DockPanel();
		
		ListView taskView = new ListView();
		
		public Dictionary<string, bool> DisplayedTokens {
			get { return displayedTokens; }
		}
		
		public int SelectedScopeIndex {
			get { return selectedScopeIndex; }
			set { selectedScopeIndex = value;
				if (this.IsInitialized)
					UpdateItems();
			}
		}
		
		public override object Control {
			get {
				return contentPanel;
			}
		}
		
		public static TaskListPad Instance {
			get { return instance; }
		}
		
		public TaskListPad()
		{
			instance = this;
			this.displayedTokens = new Dictionary<string, bool>();
			this.tasks = new ObservableCollection<SDTask>();
			
			InitializePadContent();

			TaskService.Cleared += TaskServiceCleared;
			TaskService.Added   += TaskServiceAdded;
			TaskService.Removed += TaskServiceRemoved;
			TaskService.InUpdateChanged += TaskServiceInUpdateChanged;
			
			SD.Workbench.ActiveViewContentChanged += WorkbenchActiveViewContentChanged;
			
			if (SD.Workbench.ActiveViewContent != null) {
				UpdateItems();
				WorkbenchActiveViewContentChanged(null, null);
			}
			
			SD.ProjectService.SolutionOpened += OnSolutionOpen;
			SD.ProjectService.SolutionClosed += OnSolutionClosed;
			SD.ProjectService.CurrentProjectChanged += ProjectServiceCurrentProjectChanged;
			
			this.isInitialized = true;
		}

		void ProjectServiceCurrentProjectChanged(object sender, EventArgs e)
		{
			if (isInitialized)
				UpdateItems();
		}

		void WorkbenchActiveViewContentChanged(object sender, EventArgs e)
		{
			if (isInitialized)
				UpdateItems();
			
			ITextEditor editor = SD.GetActiveViewContentService<ITextEditor>();
			
			if (editor != null) {
				// ensure we don't attach multiple times to the same editor
				editor.Caret.LocationChanged -= CaretPositionChanged;
				editor.Caret.LocationChanged += CaretPositionChanged;
			}
		}

		void CaretPositionChanged(object sender, EventArgs e)
		{
			if (this.selectedScopeIndex > 2)
			{
				var current = GetCurrentClass();
				
				if (oldClass == null) oldClass = current;
				
				if ((current != null) && (current.ReflectionName != oldClass.ReflectionName))
					UpdateItems();
			}
		}

		void TaskServiceInUpdateChanged(object sender, EventArgs e)
		{
			if (!TaskService.InUpdate)
				UpdateItems();
		}
		
		void InitializePadContent()
		{
			IReadOnlyList<string> tokens = SD.ParserService.TaskListTokens;
			
			foreach (string token in tokens) {
				if (!this.displayedTokens.ContainsKey(token)) {
					this.displayedTokens.Add(token, true);
				}
			}
			
			toolBar = ToolBarService.CreateToolBar(contentPanel, this, "/SharpDevelop/Pads/TaskList/Toolbar");
			var items = (IList)toolBar.ItemsSource;
			
			foreach (string token in tokens) {
				items.Add(new Separator());
				items.Add(new TaskListTokensToolbarCheckBox(token));
			}
			
			contentPanel.Children.Add(toolBar);
			toolBar.SetValue(DockPanel.DockProperty, Dock.Top);
			contentPanel.Children.Add(taskView);
			taskView.ItemsSource = tasks;
			taskView.MouseDoubleClick += TaskViewMouseDoubleClick;
			taskView.Style = (Style)new TaskViewResources()["TaskListView"];
			taskView.ContextMenu = MenuService.CreateContextMenu(taskView, DefaultContextMenuAddInTreeEntry);
		}
		
		void TaskViewMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			SDTask task = taskView.SelectedItem as SDTask;
			var item = taskView.ItemContainerGenerator.ContainerFromItem(task) as ListViewItem;
			UIElement element = e.MouseDevice.DirectlyOver as UIElement;
			if (task != null && task.FileName != null && element != null && item != null
			    && element.IsDescendantOf(item)) {
				SD.FileService.JumpToFilePosition(task.FileName, task.Line, task.Column);
			}
		}
		
		public void UpdateItems()
		{
			tasks.Clear();
			
			foreach (SDTask t in TaskService.CommentTasks) {
				AddItem(t);
			}
		}
		
		void AddItem(SDTask item)
		{
			foreach (KeyValuePair<string, bool> pair in displayedTokens) {
				if (item.Description.StartsWith(pair.Key, StringComparison.Ordinal) && pair.Value && IsInScope(item))
					tasks.Add(item);
			}
		}
		
		bool IsInScope(SDTask item)
		{
			var current = GetCurrentClass();
			var itemClass = GetCurrentClass(item);
			
			switch (this.selectedScopeIndex) {
				case 0:
					// Solution
					if (ProjectService.OpenSolution != null) {
						foreach (IProject proj in ProjectService.OpenSolution.Projects) {
							if (proj.FindFile(item.FileName) != null)
								return true;
						}
					}
					return false;
				case 1:
					// Project
					return ProjectService.CurrentProject != null && ProjectService.CurrentProject.FindFile(item.FileName) != null;
				case 2:
					// All open documents
					return SD.Workbench.ViewContentCollection.Select(vc => vc.GetService<ITextEditor>()).Any(editor => editor != null && item.FileName == editor.FileName);
				case 3:
					// Document
					return SD.Workbench.ActiveViewContent != null && SD.Workbench.ActiveViewContent.PrimaryFileName == item.FileName;
				case 4:
					// Namespace
					return current != null && itemClass != null && current.Namespace == itemClass.Namespace;
				case 5:
					// Class/Module
					return current != null && itemClass != null && current == itemClass;
			}
			
			return true;
		}
		
		IUnresolvedTypeDefinition GetCurrentClass()
		{
			if (SD.Workbench.ActiveViewContent == null || SD.Workbench.ActiveViewContent.PrimaryFileName == null)
				return null;
			
			IUnresolvedFile parseInfo = SD.ParserService.GetExistingUnresolvedFile(SD.Workbench.ActiveViewContent.PrimaryFileName);
			if (parseInfo != null) {
				IPositionable positionable = SD.Workbench.ActiveViewContent.GetService<IPositionable>();
				if (positionable != null) {
					var c = parseInfo.GetInnermostTypeDefinition(positionable.Line, positionable.Column);
					if (c != null) return c;
				}
			}
			
			return null;
		}
		
		IUnresolvedTypeDefinition GetCurrentClass(SDTask item)
		{
			// Tasks are created by parsing, so the parse information for item.FileName should already be present.
			// If they aren't, that's because the file might have been deleted/renamed in the meantime.
			// We use GetExistingParseInformation to avoid trying to parse a file that might have been deleted/renamed.
			IUnresolvedFile parseInfo = SD.ParserService.GetExistingUnresolvedFile(item.FileName);
			if (parseInfo != null) {
				var c = parseInfo.GetInnermostTypeDefinition(item.Line, item.Column);
				if (c != null) return c;
			}
			
			return null;
		}
		
		void OnSolutionOpen(object sender, SolutionEventArgs e)
		{
			tasks.Clear();
		}
		
		void OnSolutionClosed(object sender, EventArgs e)
		{
			tasks.Clear();
		}
		
		void TaskServiceCleared(object sender, EventArgs e)
		{
			tasks.Clear();
		}
		
		void TaskServiceAdded(object sender, TaskEventArgs e)
		{
			if (e.Task.TaskType == TaskType.Comment) {
				AddItem(e.Task);
			}
		}
		
		void TaskServiceRemoved(object sender, TaskEventArgs e)
		{
			if (e.Task.TaskType == TaskType.Comment) {
				tasks.Remove(e.Task);
			}
		}
		
		#region IClipboardHandler interface implementation
		public bool EnableCut {
			get { return false; }
		}
		public bool EnableCopy {
			get { return taskView.SelectedItem != null; }
		}
		public bool EnablePaste {
			get { return false; }
		}
		public bool EnableDelete {
			get { return false; }
		}
		public bool EnableSelectAll {
			get { return true; }
		}
		
		public void Cut() {}
		public void Paste() {}
		public void Delete() {}
		
		public void Copy()
		{
			TaskViewResources.CopySelectionToClipboard(taskView);
		}
		public void SelectAll()
		{
			taskView.SelectAll();
		}
		#endregion
	}

}
