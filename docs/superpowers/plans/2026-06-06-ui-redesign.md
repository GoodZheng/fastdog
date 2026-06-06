# FastDog UI 现代化改造 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 按照设计文档 `docs/superpowers/specs/2026-06-06-ui-redesign-design.md` 重写 FastDog 的 WPF 界面，实现现代化外观。

**Architecture:** 纯 XAML 样式重写，不引入第三方 UI 框架。颜色主题和全局样式定义在 App.xaml，MainWindow.xaml 完整重写布局，ViewModel 添加少量新属性。

**Tech Stack:** WPF (.NET 8), CommunityToolkit.Mvvm 8.4, AvalonEdit 6.3

**注意:** 这是纯 UI 层改造，没有传统意义的单元测试。每个 task 的验证方式是成功编译 (`dotnet build`)。

---

## File Structure

| 操作 | 文件 | 说明 |
|------|------|------|
| Modify | `src/FastDog/App.xaml` | 添加颜色主题资源和全局样式 |
| Modify | `src/FastDog/ViewModels/MainViewModel.cs` | 添加标签切换属性、历史命令、显示属性 |
| Rewrite | `src/FastDog/MainWindow.xaml` | 完整重写布局 |
| Modify | `src/FastDog/MainWindow.xaml.cs` | 添加内联编辑和标签切换处理 |
| No change | Services/, Models/, Tests | 不动 |

---

### Task 1: App.xaml — Color theme and global styles

**Files:**
- Modify: `src/FastDog/App.xaml`

- [ ] **Step 1: Rewrite App.xaml with all color resources and styles**

Replace entire file with:

```xml
<Application x:Class="FastDog.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:FastDog.ViewModels"
             StartupUri="MainWindow.xaml">
    <Application.Resources>
        <vm:MainViewModel x:Key="MainViewModel" />

        <!-- ========== Color Theme ========== -->
        <SolidColorBrush x:Key="AccentBrush" Color="#3498db"/>
        <SolidColorBrush x:Key="DarkBrush" Color="#2c3e50"/>
        <SolidColorBrush x:Key="DangerBrush" Color="#e74c3c"/>
        <SolidColorBrush x:Key="SuccessBrush" Color="#27ae60"/>
        <SolidColorBrush x:Key="WarningBrush" Color="#f39c12"/>
        <SolidColorBrush x:Key="GrayLightBrush" Color="#ecf0f1"/>
        <SolidColorBrush x:Key="GrayMidBrush" Color="#bdc3c7"/>
        <SolidColorBrush x:Key="GrayTextBrush" Color="#7f8c8d"/>
        <SolidColorBrush x:Key="MutedTextBrush" Color="#999999"/>
        <SolidColorBrush x:Key="HighlightBgBrush" Color="#fef9e7"/>
        <SolidColorBrush x:Key="HighlightFgBrush" Color="#e67e22"/>
        <SolidColorBrush x:Key="HoverBgBrush" Color="#f8f9fa"/>
        <SolidColorBrush x:Key="SelectedRowBrush" Color="#eaf2f8"/>

        <!-- ========== Converters ========== -->
        <BooleanToVisibilityConverter x:Key="BoolToVis"/>

        <!-- ========== ToggleOptionButton (搜索选项按钮) ========== -->
        <Style x:Key="ToggleOptionButton" TargetType="ToggleButton">
            <Setter Property="Padding" Value="10,5"/>
            <Setter Property="Margin" Value="0,0,4,0"/>
            <Setter Property="FontSize" Value="11"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="ToggleButton">
                        <Border x:Name="border"
                                Background="#fff" BorderBrush="#ddd" BorderThickness="1"
                                CornerRadius="4" Padding="{TemplateBinding Padding}"
                                SnapsToDevicePixels="True">
                            <ContentPresenter x:Name="content"
                                              HorizontalAlignment="Center" VerticalAlignment="Center"
                                              TextBlock.Foreground="#555"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsChecked" Value="True">
                                <Setter TargetName="border" Property="Background" Value="{StaticResource AccentBrush}"/>
                                <Setter TargetName="border" Property="BorderBrush" Value="{StaticResource AccentBrush}"/>
                                <Setter TargetName="content" Property="TextBlock.Foreground" Value="White"/>
                            </Trigger>
                            <MultiTrigger>
                                <MultiTrigger.Conditions>
                                    <Condition Property="IsMouseOver" Value="True"/>
                                    <Condition Property="IsChecked" Value="False"/>
                                </MultiTrigger.Conditions>
                                <Setter TargetName="border" Property="Background" Value="#f0f0f0"/>
                            </MultiTrigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- ========== TabButtonStyle (标签栏按钮) ========== -->
        <Style x:Key="TabButtonStyle" TargetType="RadioButton">
            <Setter Property="Focusable" Value="False"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="FontSize" Value="12"/>
            <Setter Property="Foreground" Value="#7f8c8d"/>
            <Setter Property="Padding" Value="18,7"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="RadioButton">
                        <Border x:Name="border" Padding="{TemplateBinding Padding}"
                                BorderBrush="Transparent" BorderThickness="0,0,0,2"
                                Margin="0,0,0,-2">
                            <ContentPresenter x:Name="content" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsChecked" Value="True">
                                <Setter TargetName="border" Property="BorderBrush" Value="{StaticResource AccentBrush}"/>
                                <Setter Property="Foreground" Value="#2c3e50"/>
                                <Setter Property="FontWeight" Value="SemiBold"/>
                            </Trigger>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter Property="Foreground" Value="#2c3e50"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- ========== SearchInput (搜索输入框) ========== -->
        <Style x:Key="SearchInput" TargetType="TextBox">
            <Setter Property="BorderBrush" Value="#ddd"/>
            <Setter Property="BorderThickness" Value="2"/>
            <Setter Property="Padding" Value="6,5"/>
            <Setter Property="FontSize" Value="13"/>
            <Setter Property="Background" Value="White"/>
            <Style.Triggers>
                <Trigger Property="IsFocused" Value="True">
                    <Setter Property="BorderBrush" Value="#3498db"/>
                </Trigger>
            </Style.Triggers>
        </Style>

        <!-- ========== ActionButton (浏览等按钮) ========== -->
        <Style x:Key="ActionButton" TargetType="Button">
            <Setter Property="Padding" Value="12,5"/>
            <Setter Property="FontSize" Value="12"/>
            <Setter Property="Background" Value="#fff"/>
            <Setter Property="BorderBrush" Value="#ddd"/>
            <Setter Property="BorderThickness" Value="1"/>
            <Setter Property="Cursor" Value="Hand"/>
        </Style>

        <!-- ========== SearchButton (搜索按钮) ========== -->
        <Style x:Key="SearchButton" TargetType="Button">
            <Setter Property="Padding" Value="20,5"/>
            <Setter Property="FontSize" Value="11"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
            <Setter Property="Background" Value="#27ae60"/>
            <Setter Property="Foreground" Value="White"/>
            <Setter Property="BorderBrush" Value="#27ae60"/>
            <Setter Property="BorderThickness" Value="1"/>
            <Setter Property="Cursor" Value="Hand"/>
        </Style>

        <!-- ========== CancelButton (取消按钮) ========== -->
        <Style x:Key="CancelButton" TargetType="Button">
            <Setter Property="Padding" Value="14,5"/>
            <Setter Property="FontSize" Value="11"/>
            <Setter Property="Background" Value="#e74c3c"/>
            <Setter Property="Foreground" Value="White"/>
            <Setter Property="BorderBrush" Value="#e74c3c"/>
            <Setter Property="BorderThickness" Value="1"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Margin" Value="0,0,6,0"/>
        </Style>

        <!-- ========== TagButton (文件过滤/排除 标签按钮) ========== -->
        <Style x:Key="TagButton" TargetType="Button">
            <Setter Property="Padding" Value="8,4"/>
            <Setter Property="FontSize" Value="11"/>
            <Setter Property="Background" Value="#fff"/>
            <Setter Property="Foreground" Value="#555"/>
            <Setter Property="BorderBrush" Value="#ddd"/>
            <Setter Property="BorderThickness" Value="1"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Margin" Value="0,0,4,0"/>
        </Style>

        <!-- ========== TagInput (文件过滤/排除 内联编辑输入框) ========== -->
        <Style x:Key="TagInput" TargetType="TextBox">
            <Setter Property="Padding" Value="4,4"/>
            <Setter Property="FontSize" Value="11"/>
            <Setter Property="BorderBrush" Value="{StaticResource AccentBrush}"/>
            <Setter Property="BorderThickness" Value="2"/>
            <Setter Property="Margin" Value="0,0,4,0"/>
        </Style>

        <!-- ========== PanelHeader (面板标题) ========== -->
        <Style x:Key="PanelHeader" TargetType="Border">
            <Setter Property="Background" Value="#ecf0f1"/>
            <Setter Property="Padding" Value="10,6"/>
            <Setter Property="BorderBrush" Value="#ddd"/>
            <Setter Property="BorderThickness" Value="0,0,0,1"/>
        </Style>

    </Application.Resources>
</Application>
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build FastDog.sln`
Expected: Build succeeded. (No runtime changes yet — just style definitions)

- [ ] **Step 3: Commit**

```bash
git add src/FastDog/App.xaml
git commit -m "style: add color theme and global styles to App.xaml"
```

---

### Task 2: MainViewModel — New properties and commands

**Files:**
- Modify: `src/FastDog/ViewModels/MainViewModel.cs`

- [ ] **Step 1: Add new properties and commands**

Add these to `MainViewModel.cs`:

**a) Tab switching property** — add after the `_isSessionRestored` field:

```csharp
[ObservableProperty] private bool _isHistoryTab;
```

This single property controls tab state. `IsHistoryTab = false` means results tab is active.

**b) Display properties for file filter/exclude buttons** — add as computed properties:

```csharp
public string FileFilterDisplay => string.IsNullOrEmpty(FileFilter) ? "文件: *" : $"文件: {FileFilter}";
public string ExcludeDirsDisplay => string.IsNullOrEmpty(ExcludeDirs) ? "排除: (无)" : $"排除: {ExcludeDirs}";
```

**c) Update existing partial methods to notify display properties** — replace the existing `OnFileFilterChanged` partial and add `OnExcludeDirsChanged`:

```csharp
partial void OnFileFilterChanged(string value)
{
    ClearSessionRestore();
    OnPropertyChanged(nameof(FileFilterDisplay));
}

partial void OnExcludeDirsChanged(string value)
{
    OnPropertyChanged(nameof(ExcludeDirsDisplay));
}
```

Note: `OnExcludeDirsChanged` is a new partial method (CommunityToolkit.Mvvm auto-generates the declaration for all `[ObservableProperty]` fields).

**d) Add new history commands** — add after existing `ClearHistory` command:

```csharp
[RelayCommand]
private void UseHistoryEntry(SearchHistoryEntry entry)
{
    RestoreFromEntry(entry);
}

[RelayCommand]
private void SearchWithHistory(SearchHistoryEntry entry)
{
    RestoreFromEntry(entry);
    _ = SearchAsync();
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build FastDog.sln`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/FastDog/ViewModels/MainViewModel.cs
git commit -m "feat: add tab switching, display properties, and parameterized history commands"
```

---

### Task 3: MainWindow.xaml — Complete layout rewrite

**Files:**
- Rewrite: `src/FastDog/MainWindow.xaml`

This is the main task. The entire XAML is rewritten to match the target design. The old file is replaced completely.

- [ ] **Step 1: Write the complete new MainWindow.xaml**

Replace entire file content with:

```xml
<Window x:Class="FastDog.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:avalonedit="http://icsharpcode.net/sharpdevelop/avalonedit"
        xmlns:models="clr-namespace:FastDog.Models"
        Title="FastDog — 基于 ripgrep 的文本搜索" Height="650" Width="1000"
        AllowDrop="True" DragOver="Window_DragOver" Drop="Window_Drop"
        DataContext="{StaticResource MainViewModel}"
        Background="#f5f5f5">
    <DockPanel>

        <!-- ==================== Header: 搜索条件区 ==================== -->
        <Border DockPanel.Dock="Top" Background="#fff"
                BorderBrush="#e0e0e0" BorderThickness="0,0,0,2"
                Padding="14,14,14,10">
            <StackPanel>
                <!-- Logo -->
                <StackPanel Orientation="Horizontal" Margin="0,0,0,10">
                    <TextBlock Text="Fast" FontWeight="ExtraBold" FontSize="18" Foreground="#e74c3c"/>
                    <TextBlock Text="Dog" FontWeight="ExtraBold" FontSize="18" Foreground="#2c3e50"/>
                    <TextBlock Text="v1.0 — 基于 ripgrep 的文本搜索" FontSize="11" Foreground="#999"
                               VerticalAlignment="Center" Margin="6,0,0,0"/>
                </StackPanel>

                <!-- 搜索路径 -->
                <DockPanel Margin="0,0,0,6">
                    <TextBlock Text="搜索路径" FontSize="11" Foreground="#999"
                               Width="55" VerticalAlignment="Center"/>
                    <Button DockPanel.Dock="Right" Content="浏览..."
                            Command="{Binding BrowseCommand}"
                            Style="{StaticResource ActionButton}" Margin="6,0,0,0"/>
                    <TextBox Text="{Binding SearchPath, UpdateSourceTrigger=PropertyChanged}"
                             Style="{StaticResource SearchInput}"/>
                </DockPanel>

                <!-- 搜索内容 -->
                <DockPanel Margin="0,0,0,8">
                    <TextBlock Text="搜索内容" FontSize="11" Foreground="#999"
                               Width="55" VerticalAlignment="Center"/>
                    <TextBox Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}"
                             Style="{StaticResource SearchInput}"/>
                </DockPanel>

                <!-- 按钮行 -->
                <WrapPanel Orientation="Horizontal">
                    <!-- 搜索模式 -->
                    <RadioButton GroupName="SearchMode"
                                 Style="{StaticResource ToggleOptionButton}"
                                 IsChecked="{Binding IsRegex}"
                                 Content="正则表达式"/>
                    <RadioButton GroupName="SearchMode"
                                 Style="{StaticResource ToggleOptionButton}"
                                 IsChecked="{Binding IsPlainText}"
                                 Content="纯文本"/>

                    <!-- 选项 -->
                    <CheckBox Style="{StaticResource ToggleOptionButton}"
                              IsChecked="{Binding CaseSensitive}"
                              Content="区分大小写 (Aa)"/>
                    <CheckBox Style="{StaticResource ToggleOptionButton}"
                              IsChecked="{Binding WholeWord}"
                              Content="全词匹配"/>

                    <!-- 分隔线 -->
                    <TextBlock Text="|" Foreground="#ccc" Margin="4,0,4,0"
                               VerticalAlignment="Center" FontSize="11"/>

                    <!-- 文件过滤 -->
                    <Grid Width="120" Margin="0,0,4,0">
                        <Button x:Name="FileFilterButton"
                                Style="{StaticResource TagButton}"
                                Content="{Binding FileFilterDisplay}"
                                Click="FileFilterButton_Click"
                                HorizontalAlignment="Stretch"/>
                        <TextBox x:Name="FileFilterTextBox"
                                 Style="{StaticResource TagInput}"
                                 Text="{Binding FileFilter, UpdateSourceTrigger=PropertyChanged}"
                                 Visibility="Collapsed"
                                 LostFocus="FileFilterTextBox_LostFocus"
                                 KeyDown="FileFilterTextBox_KeyDown"
                                 HorizontalAlignment="Stretch"/>
                    </Grid>

                    <!-- 排除目录 -->
                    <Grid Width="140" Margin="0,0,4,0">
                        <Button x:Name="ExcludeButton"
                                Style="{StaticResource TagButton}"
                                Content="{Binding ExcludeDirsDisplay}"
                                Click="ExcludeButton_Click"
                                HorizontalAlignment="Stretch"/>
                        <TextBox x:Name="ExcludeTextBox"
                                 Style="{StaticResource TagInput}"
                                 Text="{Binding ExcludeDirs, UpdateSourceTrigger=PropertyChanged}"
                                 Visibility="Collapsed"
                                 LostFocus="ExcludeTextBox_LostFocus"
                                 KeyDown="ExcludeTextBox_KeyDown"
                                 HorizontalAlignment="Stretch"/>
                    </Grid>

                    <!-- 分隔线 -->
                    <TextBlock Text="|" Foreground="#ccc" Margin="4,0,4,0"
                               VerticalAlignment="Center" FontSize="11"/>

                    <!-- 日期范围 -->
                    <CheckBox x:Name="DateToggle"
                              Style="{StaticResource ToggleOptionButton}"
                              IsChecked="{Binding DateFilterEnabled}"
                              Content="&#x1F4C5; 日期范围"/>

                    <!-- 弹性空间 -->
                    <Border Width="10"/>

                    <!-- 操作按钮 -->
                    <WrapPanel Orientation="Horizontal" HorizontalAlignment="Right">
                        <Button Content="取消" Command="{Binding CancelCommand}"
                                Style="{StaticResource CancelButton}"/>
                        <Button Content="搜索" Command="{Binding SearchCommand}"
                                Style="{StaticResource SearchButton}" IsDefault="True"/>
                    </WrapPanel>
                </WrapPanel>

                <!-- 日期选择行（折叠） -->
                <StackPanel Orientation="Horizontal" Margin="55,6,0,0">
                    <StackPanel.Style>
                        <Style TargetType="StackPanel">
                            <Setter Property="Visibility" Value="Collapsed"/>
                            <Style.Triggers>
                                <DataTrigger Binding="{Binding DateFilterEnabled}" Value="True">
                                    <Setter Property="Visibility" Value="Visible"/>
                                </DataTrigger>
                            </Style.Triggers>
                        </Style>
                    </StackPanel.Style>
                    <DatePicker SelectedDate="{Binding DateFrom}" Width="120"/>
                    <TextBlock Text=" ~ " VerticalAlignment="Center" Foreground="#999"/>
                    <DatePicker SelectedDate="{Binding DateTo}" Width="120"/>
                </StackPanel>
            </StackPanel>
        </Border>

        <!-- ==================== Tab Bar ==================== -->
        <Border DockPanel.Dock="Top" Background="#ecf0f1"
                BorderBrush="#bdc3c7" BorderThickness="0,0,0,2">
            <StackPanel Orientation="Horizontal" Margin="18,0,0,0">
                <RadioButton x:Name="TabResults" GroupName="MainTabs"
                             Style="{StaticResource TabButtonStyle}"
                             IsChecked="True"
                             Checked="TabResults_Checked">
                    <TextBlock Text="搜索结果"/>
                </RadioButton>
                <RadioButton x:Name="TabHistory" GroupName="MainTabs"
                             Style="{StaticResource TabButtonStyle}"
                             Checked="TabHistory_Checked">
                    <StackPanel Orientation="Horizontal">
                        <TextBlock Text="搜索历史" VerticalAlignment="Center"/>
                        <Border CornerRadius="7" Padding="5,1" Margin="4,0,0,0">
                            <Border.Style>
                                <Style TargetType="Border">
                                    <Setter Property="Background" Value="#bdc3c7"/>
                                    <Style.Triggers>
                                        <DataTrigger Binding="{Binding IsChecked, ElementName=TabHistory}" Value="True">
                                            <Setter Property="Background" Value="#3498db"/>
                                        </DataTrigger>
                                    </Style.Triggers>
                                </Style>
                            </Border.Style>
                            <TextBlock Text="{Binding HistoryEntries.Count, FallbackValue=0}"
                                       FontSize="9" Foreground="White" VerticalAlignment="Center"/>
                        </Border>
                    </StackPanel>
                </RadioButton>
            </StackPanel>
        </Border>

        <!-- ==================== Status Bar ==================== -->
        <Border DockPanel.Dock="Bottom" Background="#2c3e50" Padding="8,5">
            <Grid>
                <TextBlock Foreground="#ecf0f1" FontSize="11" VerticalAlignment="Center">
                    <Run Text="{Binding StatusText, Mode=OneWay}"/>
                </TextBlock>
                <StackPanel HorizontalAlignment="Right" Orientation="Horizontal">
                    <TextBlock Foreground="#ecf0f1" FontSize="11" VerticalAlignment="Center">
                        <Run Text="文件: "/><Run Text="{Binding TotalFiles, Mode=OneWay}" Foreground="#3498db" FontWeight="Bold"/>
                    </TextBlock>
                    <TextBlock Foreground="#ecf0f1" FontSize="11" Margin="8,0,0,0" VerticalAlignment="Center">
                        <Run Text="匹配: "/><Run Text="{Binding TotalMatches, Mode=OneWay}" Foreground="#3498db" FontWeight="Bold"/>
                    </TextBlock>
                    <TextBlock Foreground="#ecf0f1" FontSize="11" Margin="8,0,0,0" VerticalAlignment="Center">
                        <Run Text="{Binding ElapsedTime, StringFormat='{}{0}', Mode=OneWay}" Foreground="#3498db" FontWeight="Bold"/>
                    </TextBlock>
                </StackPanel>
            </Grid>
        </Border>

        <!-- ==================== Content Area ==================== -->
        <Grid>
            <!-- ===== 搜索结果视图 ===== -->
            <Grid x:Name="ResultsView">
                <Grid.Style>
                    <Style TargetType="Grid">
                        <Setter Property="Visibility" Value="Visible"/>
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding IsHistoryTab}" Value="True">
                                <Setter Property="Visibility" Value="Collapsed"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </Grid.Style>
                <Grid.RowDefinitions>
                    <RowDefinition Height="3*"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="2*"/>
                </Grid.RowDefinitions>

                <!-- 文件列表 -->
                <DataGrid Grid.Row="0"
                          ItemsSource="{Binding SearchResults}"
                          SelectedItem="{Binding SelectedResult}"
                          AutoGenerateColumns="False"
                          IsReadOnly="True"
                          SelectionMode="Single"
                          HeadersVisibility="Column"
                          GridLinesVisibility="Horizontal"
                          HorizontalGridLinesBrush="#ecf0f1"
                          BorderThickness="0"
                          Background="White"
                          RowBackground="White"
                          AlternatingRowBackground="White"
                          MouseDoubleClick="DataGrid_MouseDoubleClick">
                    <DataGrid.Resources>
                        <Style TargetType="DataGridColumnHeader">
                            <Setter Property="Background" Value="#ecf0f1"/>
                            <Setter Property="Foreground" Value="#7f8c8d"/>
                            <Setter Property="FontWeight" Value="SemiBold"/>
                            <Setter Property="FontSize" Value="11"/>
                            <Setter Property="Padding" Value="8,7"/>
                            <Setter Property="BorderBrush" Value="#bdc3c7"/>
                            <Setter Property="BorderThickness" Value="0,0,0,2"/>
                        </Style>
                        <Style TargetType="DataGridRow">
                            <Setter Property="Cursor" Value="Hand"/>
                            <Style.Triggers>
                                <Trigger Property="IsMouseOver" Value="True">
                                    <Setter Property="Background" Value="#f8f9fa"/>
                                </Trigger>
                                <Trigger Property="IsSelected" Value="True">
                                    <Setter Property="Background" Value="#eaf2f8"/>
                                </Trigger>
                            </Style.Triggers>
                        </Style>
                        <Style TargetType="DataGridCell">
                            <Setter Property="BorderThickness" Value="0"/>
                            <Setter Property="Padding" Value="4,0"/>
                            <Setter Property="Focusable" Value="False"/>
                            <Style.Triggers>
                                <Trigger Property="IsSelected" Value="True">
                                    <Setter Property="Foreground" Value="#2c3e50"/>
                                </Trigger>
                            </Style.Triggers>
                        </Style>
                    </DataGrid.Resources>
                    <DataGrid.ContextMenu>
                        <ContextMenu>
                            <MenuItem Header="打开文件" Command="{Binding OpenFileCommand}"/>
                            <MenuItem Header="复制路径" Command="{Binding CopyPathCommand}"/>
                            <MenuItem Header="复制文件名" Command="{Binding CopyFileNameCommand}"/>
                        </ContextMenu>
                    </DataGrid.ContextMenu>
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="文件名" Binding="{Binding FileName}" Width="200">
                            <DataGridTextColumn.ElementStyle>
                                <Style TargetType="TextBlock">
                                    <Setter Property="FontWeight" Value="Medium"/>
                                    <Setter Property="Foreground" Value="#2c3e50"/>
                                </Style>
                            </DataGridTextColumn.ElementStyle>
                        </DataGridTextColumn>
                        <DataGridTextColumn Header="大小" Binding="{Binding FileSize, StringFormat='{}{0:N0}'}" Width="80">
                            <DataGridTextColumn.ElementStyle>
                                <Style TargetType="TextBlock">
                                    <Setter Property="Foreground" Value="#555"/>
                                </Style>
                            </DataGridTextColumn.ElementStyle>
                        </DataGridTextColumn>
                        <DataGridTemplateColumn Header="匹配数" Width="70">
                            <DataGridTemplateColumn.CellTemplate>
                                <DataTemplate>
                                    <Border Background="#3498db" CornerRadius="9" Padding="6,1"
                                            HorizontalAlignment="Center">
                                        <TextBlock Text="{Binding MatchCount}" Foreground="White"
                                                   FontWeight="Bold" FontSize="10"
                                                   HorizontalAlignment="Center"/>
                                    </Border>
                                </DataTemplate>
                            </DataGridTemplateColumn.CellTemplate>
                        </DataGridTemplateColumn>
                        <DataGridTextColumn Header="路径" Binding="{Binding FilePath}" Width="*">
                            <DataGridTextColumn.ElementStyle>
                                <Style TargetType="TextBlock">
                                    <Setter Property="Foreground" Value="#999"/>
                                    <Setter Property="FontSize" Value="11"/>
                                    <Setter Property="TextTrimming" Value="CharacterEllipsis"/>
                                </Style>
                            </DataGridTextColumn.ElementStyle>
                        </DataGridTextColumn>
                        <DataGridTextColumn Header="修改时间" Binding="{Binding LastModified, StringFormat='{}{0:yyyy-MM-dd HH:mm}'}" Width="130">
                            <DataGridTextColumn.ElementStyle>
                                <Style TargetType="TextBlock">
                                    <Setter Property="Foreground" Value="#999"/>
                                    <Setter Property="FontSize" Value="11"/>
                                </Style>
                            </DataGridTextColumn.ElementStyle>
                        </DataGridTextColumn>
                    </DataGrid.Columns>
                </DataGrid>

                <!-- 分割线 -->
                <GridSplitter Grid.Row="1" Height="3" HorizontalAlignment="Stretch"
                              Background="#ecf0f1"/>

                <!-- 下半区：匹配行 + 预览 -->
                <Grid Grid.Row="2">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="35*" MinWidth="200"/>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="65*" MinWidth="300"/>
                    </Grid.ColumnDefinitions>

                    <!-- 左侧：匹配行 -->
                    <Grid Grid.Column="0">
                        <Grid.RowDefinitions>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="*"/>
                        </Grid.RowDefinitions>
                        <Border Grid.Row="0" Style="{StaticResource PanelHeader}">
                            <StackPanel Orientation="Horizontal">
                                <TextBlock Text="匹配行" FontWeight="SemiBold" FontSize="11" Foreground="#7f8c8d"/>
                                <TextBlock Text="{Binding SelectedResult.MatchCount, FallbackValue=0}"
                                           FontWeight="Bold" FontSize="11" Foreground="#e74c3c" Margin="4,0"/>
                                <TextBlock Text="—" FontSize="11" Foreground="#7f8c8d" Margin="4,0"/>
                                <TextBlock Text="{Binding SelectedResult.FileName, FallbackValue=''}"
                                           FontSize="11" Foreground="#7f8c8d"/>
                            </StackPanel>
                        </Border>
                        <ListBox Grid.Row="1"
                                 ItemsSource="{Binding MatchLines}"
                                 SelectedItem="{Binding SelectedMatchLine}"
                                 MouseDoubleClick="MatchList_MouseDoubleClick"
                                 BorderThickness="0"
                                 FontFamily="Consolas" FontSize="11"
                                 Background="White">
                            <ListBox.Resources>
                                <Style TargetType="ListBoxItem">
                                    <Setter Property="Padding" Value="2,1"/>
                                    <Setter Property="Cursor" Value="Hand"/>
                                    <Style.Triggers>
                                        <Trigger Property="IsSelected" Value="True">
                                            <Setter Property="Background" Value="#fef9e7"/>
                                            <Setter Property="BorderBrush" Value="#f39c12"/>
                                            <Setter Property="BorderThickness" Value="3,0,0,0"/>
                                        </Trigger>
                                        <Trigger Property="IsMouseOver" Value="True">
                                            <Setter Property="Background" Value="#f8f9fa"/>
                                        </Trigger>
                                    </Style.Triggers>
                                </Style>
                            </ListBox.Resources>
                            <ListBox.ItemTemplate>
                                <DataTemplate>
                                    <Grid>
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width="40"/>
                                            <ColumnDefinition Width="*"/>
                                        </Grid.ColumnDefinitions>
                                        <TextBlock Grid.Column="0" Text="{Binding LineNumber}"
                                                   Foreground="#bdc3c7" VerticalAlignment="Center"
                                                   HorizontalAlignment="Right" Margin="0,0,8,0"/>
                                        <TextBlock Grid.Column="1" Text="{Binding LineText}"
                                                   Foreground="#555" VerticalAlignment="Center"
                                                   TextTrimming="CharacterEllipsis"/>
                                    </Grid>
                                </DataTemplate>
                            </ListBox.ItemTemplate>
                        </ListBox>
                    </Grid>

                    <GridSplitter Grid.Column="1" Width="2" HorizontalAlignment="Stretch"
                                  Background="#ecf0f1" VerticalAlignment="Stretch"/>

                    <!-- 右侧：文件预览 -->
                    <Grid Grid.Column="2">
                        <Grid.RowDefinitions>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="*"/>
                        </Grid.RowDefinitions>
                        <Border Grid.Row="0" Style="{StaticResource PanelHeader}">
                            <StackPanel Orientation="Horizontal">
                                <TextBlock Text="文件预览" FontWeight="SemiBold" FontSize="11" Foreground="#7f8c8d"/>
                                <TextBlock Text=" — " FontSize="11" Foreground="#7f8c8d"/>
                                <TextBlock Text="{Binding SelectedResult.FileName, FallbackValue=''}"
                                           FontSize="11" Foreground="#7f8c8d"/>
                            </StackPanel>
                        </Border>
                        <Grid Grid.Row="1">
                            <avalonedit:TextEditor Name="PreviewEditor"
                                IsReadOnly="True"
                                ShowLineNumbers="True"
                                WordWrap="False"
                                FontFamily="Consolas"
                                FontSize="12"/>

                            <TextBlock Text="二进制文件，无法预览"
                                       HorizontalAlignment="Center" VerticalAlignment="Center"
                                       Foreground="Gray" FontSize="14">
                                <TextBlock.Visibility>
                                    <Binding Path="IsBinaryFile">
                                        <Binding.Converter>
                                            <BooleanToVisibilityConverter/>
                                        </Binding.Converter>
                                    </Binding>
                                </TextBlock.Visibility>
                            </TextBlock>

                            <TextBlock Text="{Binding FileErrorMessage}"
                                       HorizontalAlignment="Center" VerticalAlignment="Center"
                                       Foreground="Gray" FontSize="14">
                                <TextBlock.Visibility>
                                    <Binding Path="IsFileError">
                                        <Binding.Converter>
                                            <BooleanToVisibilityConverter/>
                                        </Binding.Converter>
                                    </Binding>
                                </TextBlock.Visibility>
                            </TextBlock>

                            <TextBlock Text="文件过大，仅显示部分内容"
                                       HorizontalAlignment="Right" VerticalAlignment="Top"
                                       Foreground="#f39c12" FontSize="11" Margin="4"
                                       Panel.ZIndex="1">
                                <TextBlock.Visibility>
                                    <Binding Path="IsFileTruncated">
                                        <Binding.Converter>
                                            <BooleanToVisibilityConverter/>
                                        </Binding.Converter>
                                    </Binding>
                                </TextBlock.Visibility>
                            </TextBlock>
                        </Grid>
                    </Grid>
                </Grid>
            </Grid>

            <!-- ===== 搜索历史视图 ===== -->
            <Grid x:Name="HistoryView">
                <Grid.Style>
                    <Style TargetType="Grid">
                        <Setter Property="Visibility" Value="Collapsed"/>
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding IsHistoryTab}" Value="True">
                                <Setter Property="Visibility" Value="Visible"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </Grid.Style>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="*"/>
                </Grid.RowDefinitions>

                <!-- 工具栏 -->
                <Border Grid.Row="0" Background="#fafafa" Padding="16,8"
                        BorderBrush="#ecf0f1" BorderThickness="0,0,0,1">
                    <DockPanel>
                        <TextBlock Text="搜索历史" FontWeight="SemiBold" FontSize="13" Foreground="#2c3e50"
                                   VerticalAlignment="Center"/>
                        <TextBlock Text="点击任意记录可恢复搜索条件" FontSize="11" Foreground="#999"
                                   Margin="10,0,0,0" VerticalAlignment="Center"/>
                        <Button DockPanel.Dock="Right" Content="清空历史"
                                Command="{Binding ClearHistoryCommand}"
                                Foreground="#e74c3c" Background="Transparent"
                                BorderThickness="0" FontSize="11" Cursor="Hand" Padding="8,4"/>
                    </DockPanel>
                </Border>

                <!-- 历史卡片列表 -->
                <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto">
                    <ItemsControl ItemsSource="{Binding HistoryEntries}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate DataType="{x:Type models:SearchHistoryEntry}">
                                <Border BorderBrush="#f0f0f0" BorderThickness="0,0,0,1"
                                        Padding="16,10" Background="Transparent"
                                        MouseLeftButtonUp="HistoryCard_Click"
                                        MouseDoubleClick="HistoryCard_DoubleClick"
                                        Cursor="Hand">
                                    <Border.Style>
                                        <Style TargetType="Border">
                                            <Style.Triggers>
                                                <Trigger Property="IsMouseOver" Value="True">
                                                    <Setter Property="Background" Value="#f8f9fa"/>
                                                </Trigger>
                                            </Style.Triggers>
                                        </Style>
                                    </Border.Style>
                                    <Grid>
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width="*"/>
                                            <ColumnDefinition Width="Auto"/>
                                            <ColumnDefinition Width="90"/>
                                        </Grid.ColumnDefinitions>

                                        <!-- 左: 搜索词+路径+标签 -->
                                        <StackPanel Grid.Column="0">
                                            <TextBlock Text="{Binding SearchText}"
                                                       FontFamily="Consolas" FontWeight="Bold"
                                                       FontSize="13" Foreground="#2c3e50"/>
                                            <TextBlock Text="{Binding SearchPath}"
                                                       FontSize="11" Foreground="#999" Margin="0,2,0,4"/>
                                            <WrapPanel>
                                                <!-- 正则/文本标签 -->
                                                <Border CornerRadius="3" Padding="4,2" Margin="0,0,4,0">
                                                    <Border.Style>
                                                        <Style TargetType="Border">
                                                            <Setter Property="Background" Value="#ecf0f1"/>
                                                            <Style.Triggers>
                                                                <DataTrigger Binding="{Binding IsRegex}" Value="True">
                                                                    <Setter Property="Background" Value="#eaf2f8"/>
                                                                </DataTrigger>
                                                            </Style.Triggers>
                                                        </Style>
                                                    </Border.Style>
                                                    <TextBlock FontSize="9">
                                                        <TextBlock.Style>
                                                            <Style TargetType="TextBlock">
                                                                <Setter Property="Foreground" Value="#7f8c8d"/>
                                                                <Setter Property="Text" Value="文本"/>
                                                                <Style.Triggers>
                                                                    <DataTrigger Binding="{Binding IsRegex}" Value="True">
                                                                        <Setter Property="Foreground" Value="#3498db"/>
                                                                        <Setter Property="Text" Value="正则"/>
                                                                    </DataTrigger>
                                                                </Style.Triggers>
                                                            </Style>
                                                        </TextBlock.Style>
                                                    </TextBlock>
                                                </Border>
                                                <!-- 区分大小写 -->
                                                <Border Background="#eaf2f8" CornerRadius="3" Padding="4,2" Margin="0,0,4,0">
                                                    <Border.Style>
                                                        <Style TargetType="Border">
                                                            <Setter Property="Visibility" Value="Collapsed"/>
                                                            <Style.Triggers>
                                                                <DataTrigger Binding="{Binding CaseSensitive}" Value="True">
                                                                    <Setter Property="Visibility" Value="Visible"/>
                                                                </DataTrigger>
                                                            </Style.Triggers>
                                                        </Style>
                                                    </Border.Style>
                                                    <TextBlock Text="区分大小写" FontSize="9" Foreground="#3498db"/>
                                                </Border>
                                                <!-- 全词 -->
                                                <Border Background="#eaf2f8" CornerRadius="3" Padding="4,2" Margin="0,0,4,0">
                                                    <Border.Style>
                                                        <Style TargetType="Border">
                                                            <Setter Property="Visibility" Value="Collapsed"/>
                                                            <Style.Triggers>
                                                                <DataTrigger Binding="{Binding WholeWord}" Value="True">
                                                                    <Setter Property="Visibility" Value="Visible"/>
                                                                </DataTrigger>
                                                            </Style.Triggers>
                                                        </Style>
                                                    </Border.Style>
                                                    <TextBlock Text="全词" FontSize="9" Foreground="#3498db"/>
                                                </Border>
                                                <!-- 文件过滤 -->
                                                <Border Background="#ecf0f1" CornerRadius="3" Padding="4,2" Margin="0,0,4,0">
                                                    <Border.Style>
                                                        <Style TargetType="Border">
                                                            <Setter Property="Visibility" Value="Visible"/>
                                                            <Style.Triggers>
                                                                <DataTrigger Binding="{Binding FileFilter}" Value="">
                                                                    <Setter Property="Visibility" Value="Collapsed"/>
                                                                </DataTrigger>
                                                            </Style.Triggers>
                                                        </Style>
                                                    </Border.Style>
                                                    <TextBlock Text="{Binding FileFilter}" FontSize="9" Foreground="#7f8c8d"/>
                                                </Border>
                                                <!-- 排除目录 -->
                                                <Border Background="#ecf0f1" CornerRadius="3" Padding="4,2" Margin="0,0,4,0">
                                                    <Border.Style>
                                                        <Style TargetType="Border">
                                                            <Setter Property="Visibility" Value="Visible"/>
                                                            <Style.Triggers>
                                                                <DataTrigger Binding="{Binding ExcludeDirs}" Value="">
                                                                    <Setter Property="Visibility" Value="Collapsed"/>
                                                                </DataTrigger>
                                                            </Style.Triggers>
                                                        </Style>
                                                    </Border.Style>
                                                    <TextBlock Text="{Binding ExcludeDirs, StringFormat='排除: {0}'}" FontSize="9" Foreground="#7f8c8d"/>
                                                </Border>
                                            </WrapPanel>
                                        </StackPanel>

                                        <!-- 中: 统计 -->
                                        <StackPanel Grid.Column="1" Orientation="Horizontal"
                                                    VerticalAlignment="Center" Margin="16,0">
                                            <TextBlock FontSize="11" Foreground="#7f8c8d">
                                                <Run Text="{Binding SearchedFiles, StringFormat='{}{0:N0}', Mode=OneWay}"
                                                     Foreground="#3498db" FontWeight="Bold"/>
                                                <Run Text=" 文件"/>
                                            </TextBlock>
                                            <TextBlock FontSize="11" Foreground="#7f8c8d" Margin="10,0,0,0">
                                                <Run Text="{Binding FoundFiles, Mode=OneWay}"
                                                     Foreground="#3498db" FontWeight="Bold"/>
                                                <Run Text=" 命中"/>
                                            </TextBlock>
                                            <TextBlock FontSize="11" Foreground="#7f8c8d" Margin="10,0,0,0">
                                                <Run Text="{Binding TotalMatches, Mode=OneWay}"
                                                     Foreground="#3498db" FontWeight="Bold"/>
                                                <Run Text=" 匹配"/>
                                            </TextBlock>
                                        </StackPanel>

                                        <!-- 右: 时间 -->
                                        <TextBlock Grid.Column="2"
                                                   Text="{Binding SearchedAt, StringFormat='{}{0:yyyy-MM-dd HH:mm}'}"
                                                   FontSize="10" Foreground="#bbb"
                                                   VerticalAlignment="Center"
                                                   HorizontalAlignment="Right"/>
                                    </Grid>
                                </Border>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </ScrollViewer>
            </Grid>
        </Grid>

    </DockPanel>
</Window>
```

- [ ] **Step 2: Build to verify (expect errors from missing code-behind handlers)**

Run: `dotnet build FastDog.sln`
Expected: Errors about missing event handlers in code-behind (FileFilterButton_Click, etc.). This is expected — Task 4 will fix them.

- [ ] **Step 3: Commit**

```bash
git add src/FastDog/MainWindow.xaml
git commit -m "style: rewrite MainWindow.xaml with modern layout"
```

---

### Task 4: MainWindow.xaml.cs — Code-behind updates

**Files:**
- Modify: `src/FastDog/MainWindow.xaml.cs`

- [ ] **Step 1: Update code-behind with new event handlers**

Replace entire file with:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FastDog.Models;
using FastDog.ViewModels;
using FastDog.Services;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;

namespace FastDog;

public partial class MainWindow : Window
{
    private MainViewModel? _vm;
    private TextMarkerService? _markerService;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        _vm = DataContext as MainViewModel;
        if (_vm is null) return;

        var editor = FindEditor();
        if (editor is null) return;

        _vm.PropertyChanged += (s, args) =>
        {
            switch (args.PropertyName)
            {
                case nameof(MainViewModel.FileContent):
                    LoadFileContent(editor, _vm);
                    break;
                case nameof(MainViewModel.FilePath):
                    ApplySyntaxHighlighting(editor, _vm.FilePath);
                    break;
            }
        };

        _vm.ScrollToLineRequested += lineNumber =>
        {
            Dispatcher.Invoke(() =>
            {
                if (lineNumber >= 1 && lineNumber <= editor.Document.LineCount)
                    editor.ScrollTo(lineNumber, 1);
            });
        };
    }

    // --- 文件预览 ---

    private void LoadFileContent(TextEditor editor, MainViewModel vm)
    {
        ClearMarkers(editor);

        if (string.IsNullOrEmpty(vm.FileContent))
        {
            editor.Document = new TextDocument();
            return;
        }

        editor.Document = new TextDocument(vm.FileContent);
        ApplyMatchMarkers(editor, vm);
    }

    private void ApplyMatchMarkers(TextEditor editor, MainViewModel vm)
    {
        if (vm.SelectedResult is null) return;

        var textArea = editor.TextArea;
        _markerService = new TextMarkerService(textArea);
        textArea.TextView.BackgroundRenderers.Add(_markerService);
        textArea.TextView.LineTransformers.Add(_markerService);

        foreach (var match in vm.SelectedResult.Matches)
        {
            if (match.GlobalMatchStart < 0 || match.GlobalMatchEnd <= match.GlobalMatchStart)
                continue;
            if (match.GlobalMatchEnd > editor.Document.TextLength)
                continue;

            _markerService.Create(match.GlobalMatchStart,
                match.GlobalMatchEnd - match.GlobalMatchStart);
        }
    }

    private void ClearMarkers(TextEditor editor)
    {
        if (_markerService is null) return;

        editor.TextArea.TextView.BackgroundRenderers.Remove(_markerService);
        editor.TextArea.TextView.LineTransformers.Remove(_markerService);
        _markerService = null;
    }

    private static void ApplySyntaxHighlighting(TextEditor editor, string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;

        var ext = System.IO.Path.GetExtension(filePath);
        var highlighting = HighlightingManager.Instance.GetDefinitionByExtension(ext);
        editor.SyntaxHighlighting = highlighting;
    }

    private TextEditor? FindEditor()
    {
        return this.FindName("PreviewEditor") as TextEditor;
    }

    // --- 双击事件 ---

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.OpenFileCommand.Execute(null);
    }

    private void MatchList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.OpenFileAtLineCommand.Execute(null);
    }

    // --- 拖放 ---

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0 && System.IO.Directory.Exists(files[0]))
            {
                if (DataContext is MainViewModel vm)
                    vm.SearchPath = files[0];
            }
        }
    }

    // --- 标签切换 ---

    private void TabResults_Checked(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.IsHistoryTab = false;
    }

    private void TabHistory_Checked(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.IsHistoryTab = true;
    }

    // --- 文件过滤内联编辑 ---

    private void FileFilterButton_Click(object sender, RoutedEventArgs e)
    {
        FileFilterButton.Visibility = Visibility.Collapsed;
        FileFilterTextBox.Visibility = Visibility.Visible;
        FileFilterTextBox.Focus();
        FileFilterTextBox.SelectAll();
    }

    private void FileFilterTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        FileFilterButton.Visibility = Visibility.Visible;
        FileFilterTextBox.Visibility = Visibility.Collapsed;
    }

    private void FileFilterTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            FileFilterButton.Visibility = Visibility.Visible;
            FileFilterTextBox.Visibility = Visibility.Collapsed;
        }
    }

    // --- 排除目录内联编辑 ---

    private void ExcludeButton_Click(object sender, RoutedEventArgs e)
    {
        ExcludeButton.Visibility = Visibility.Collapsed;
        ExcludeTextBox.Visibility = Visibility.Visible;
        ExcludeTextBox.Focus();
        ExcludeTextBox.SelectAll();
    }

    private void ExcludeTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        ExcludeButton.Visibility = Visibility.Visible;
        ExcludeTextBox.Visibility = Visibility.Collapsed;
    }

    private void ExcludeTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ExcludeButton.Visibility = Visibility.Visible;
            ExcludeTextBox.Visibility = Visibility.Collapsed;
        }
    }

    // --- 历史卡片事件 ---

    private void HistoryCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is SearchHistoryEntry entry)
        {
            if (DataContext is MainViewModel vm)
                vm.UseHistoryEntryCommand.Execute(entry);
        }
    }

    private void HistoryCard_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is SearchHistoryEntry entry)
        {
            if (DataContext is MainViewModel vm)
                vm.SearchWithHistoryCommand.Execute(entry);
        }
    }

    // --- 会话保存 ---

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.SaveSession();
        base.OnClosed(e);
    }
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build FastDog.sln`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/FastDog/MainWindow.xaml.cs
git commit -m "feat: update code-behind with inline editing, tab switching, and history card events"
```

---

### Task 5: Build, run, and verify

- [ ] **Step 1: Full build**

Run: `dotnet build FastDog.sln`
Expected: Build succeeded with 0 errors.

- [ ] **Step 2: Run the application**

Run: `dotnet run --project src/FastDog`
Expected: Window opens with new modern UI layout matching the design spec.

- [ ] **Step 3: Visual verification checklist**

Verify these visual elements match the design:
- [ ] Header: Logo "FastDog" (red/blue), search inputs, button row with toggles
- [ ] Toggle buttons highlight blue when active (正则表达式 should be active by default)
- [ ] File filter shows "文件: *" and excludes shows "排除: bin;obj" as tag buttons
- [ ] Clicking file filter/exclude button switches to inline TextBox
- [ ] Tab bar: "搜索结果" tab is active with blue underline, "搜索历史" has count badge
- [ ] DataGrid: blue badge match counts, hover highlights, selected row blue background
- [ ] Match lines: yellow highlight on selection with orange left border
- [ ] Preview panel: AvalonEdit with syntax highlighting and match markers
- [ ] Status bar: dark background (#2c3e50), blue numbers
- [ ] Switch to history tab: card layout with search text, path, tags, stats, time
- [ ] Click history card: restores search conditions
- [ ] Double-click history card: restores and searches

- [ ] **Step 4: Commit any fixes**

If any adjustments were needed during verification, commit them.
