﻿using Spotlight.Models;
using Spotlight.Renderers;
using Spotlight.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Spotlight
{
    /// <summary>
    /// Interaction logic for WorldPanel.xaml
    /// </summary>
    public partial class WorldPanel : UserControl, IDetachEvents
    {
        private World _world;
        private WorldInfo _worldInfo;
        private WorldService _worldService;
        private PalettesService _palettesService;
        private WorldRenderer _worldRenderer;
        private TileSet _tileSet;
        private TextService _textService;
        private GraphicsService _graphicsService;
        private CompressionService _compressionService;
        private TileService _tileService;
        private GraphicsAccessor _graphicsAccessor;
        private WorldDataAccessor _worldDataAccessor;
        private WriteableBitmap _bitmap;
        private HistoryService _historyService;
        private List<MapTileInteraction> _interactions;

        public delegate void WorldEditorExitSelectedHandled(int x, int y);

        public event WorldEditorExitSelectedHandled WorldEditorExitSelected;

        private bool _initializing = true;

        public WorldPanel(GraphicsService graphicsService, PalettesService palettesService, TextService textService, TileService tileService, WorldService worldService, LevelService levelService, WorldInfo worldInfo)
        {
            InitializeComponent();

            _worldInfo = worldInfo;
            _textService = textService;
            _graphicsService = graphicsService;
            _tileService = tileService;
            _palettesService = palettesService;
            _worldService = worldService;
            _historyService = new HistoryService();
            _interactions = _tileService.GetMapTileInteractions();
            _world = _worldService.LoadWorld(_worldInfo);
            _compressionService = new CompressionService();
            Tile[] bottomTableSet = _graphicsService.GetTileSection(_world.TileTableIndex);
            Tile[] topTableSet = _graphicsService.GetTileSection(_world.AnimationTileTableIndex);
            _graphicsAccessor = new GraphicsAccessor(topTableSet, bottomTableSet, _graphicsService.GetGlobalTiles(), _graphicsService.GetExtraTiles());
            _worldDataAccessor = new WorldDataAccessor(_world);

            _bitmap = new WriteableBitmap(WorldRenderer.BITMAP_WIDTH, WorldRenderer.BITMAP_HEIGHT, 96, 96, PixelFormats.Bgra32, null);
            _worldRenderer = new WorldRenderer(_graphicsAccessor, _worldDataAccessor, _palettesService, _tileService.GetMapTileInteractions());
            _worldRenderer.Initializing();

            _tileSet = _tileService.GetTileSet(_world.TileSetIndex);

            Palette palette = _palettesService.GetPalette(_world.PaletteId);
            _worldRenderer.Update(tileSet: _tileSet, palette: palette);

            WorldRenderSource.Source = _bitmap;
            WorldRenderSource.Width = _bitmap.PixelWidth;
            WorldRenderSource.Height = _bitmap.PixelHeight;
            CanvasContainer.Width = RenderContainer.Width = _world.ScreenLength * 16 * 16;

            SelectedEditMode.SelectedIndex = 0;
            SelectedDrawMode.SelectedIndex = 0;

            TileSelector.Initialize(_graphicsAccessor, _tileService, _tileSet, palette);
            PointerEditor.Initialize(levelService, _worldInfo);

            UpdateTextTables();

            _graphicsService.GraphicsUpdated += _graphicsService_GraphicsUpdated;
            _graphicsService.ExtraGraphicsUpdated += _graphicsService_GraphicsUpdated;
            _palettesService.PalettesChanged += _palettesService_PalettesChanged;
            _tileService.TileSetUpdated += _tileService_TileSetUpdated;
            _worldService.WorldUpdated += _worldService_WorldUpdated;

            _initializing = false;
            _worldRenderer.Ready();
            Update();
        }

        private void _worldService_WorldUpdated(WorldInfo worldInfo)
        {
            if (worldInfo.Id == _world.Id)
            {
                _world.Name = worldInfo.Name;
            }
        }

        private void _tileService_TileSetUpdated(int index, TileSet tileSet)
        {
            Update();
            TileSelector.Update(_tileSet);
        }

        public void DetachEvents()
        {
            _graphicsService.GraphicsUpdated -= _graphicsService_GraphicsUpdated;
            _graphicsService.ExtraGraphicsUpdated -= _graphicsService_GraphicsUpdated;
            _palettesService.PalettesChanged -= _palettesService_PalettesChanged;
            _worldService.WorldUpdated -= _worldService_WorldUpdated;
        }

        private void _palettesService_PalettesChanged()
        {
            PaletteIndex.ItemsSource = _palettesService.GetPalettes();
            if (PaletteIndex.SelectedItem == null)
            {
                PaletteIndex.SelectedIndex = 0;
            }
            else
            {
                _worldRenderer.Update(palette: (Palette)PaletteIndex.SelectedItem);
                TileSelector.Update(palette: (Palette)PaletteIndex.SelectedItem);
                Update();
            }
        }

        private void _graphicsService_GraphicsUpdated()
        {
            _graphicsAccessor.SetTopTable(_graphicsService.GetTileSection(_world.AnimationTileTableIndex));
            _graphicsAccessor.SetBottomTable(_graphicsService.GetTileSection(_world.TileTableIndex));
            TileSelector.Update();
            Update();
        }

        private void ObjectSelector_GameObjectDoubleClicked(GameObject gameObject)
        {
            GlobalPanels.EditGameObject(gameObject, (Palette)PaletteIndex.SelectedItem);
        }

        private void Update(Rect updateRect)
        {
            Update(new List<Rect>() { updateRect });
        }

        private void Update(int x = 0, int y = 0, int width = WorldRenderer.BITMAP_WIDTH, int height = WorldRenderer.BITMAP_HEIGHT)
        {
            Update(new List<Rect>() { new Rect(x, y, width, height) });
        }

        private void Update(List<Rect> updateAreas)
        {
            if (updateAreas.Count == 0 || _initializing)
            {
                return;
            }

            DateTime speedTest = DateTime.Now;

            _bitmap.Lock();

            foreach (var updateArea in updateAreas.Select(r => new Int32Rect((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height)))
            {
                Int32Rect safeRect = new Int32Rect(Math.Max(0, updateArea.X), Math.Max(0, updateArea.Y), updateArea.Width, updateArea.Height);

                if (safeRect.X + safeRect.Width > WorldRenderer.BITMAP_WIDTH)
                {
                    safeRect.Width -= WorldRenderer.BITMAP_WIDTH - (safeRect.X + safeRect.Width);
                }

                if (safeRect.Y + safeRect.Height > WorldRenderer.BITMAP_HEIGHT)
                {
                    safeRect.Height -= WorldRenderer.BITMAP_HEIGHT - (safeRect.Y + safeRect.Height);
                }

                Int32Rect sourceArea = new Int32Rect(0, 0, Math.Max(0, Math.Min(safeRect.Width, WorldRenderer.BITMAP_WIDTH)), Math.Max(0, Math.Min(safeRect.Height, WorldRenderer.BITMAP_HEIGHT)));

                _worldRenderer.Update(safeRect, withInteractionOverlay: ShowInteraction.IsChecked.Value);
                _bitmap.WritePixels(sourceArea, _worldRenderer.GetRectangle(safeRect), safeRect.Width * 4, safeRect.X, safeRect.Y);
                _bitmap.AddDirtyRect(safeRect);
            }

            _bitmap.Unlock();

            Console.WriteLine("Draw time " + (DateTime.Now - speedTest).TotalMilliseconds + " milliseconds.");
        }

        private void ClearSelectionRectangle()
        {
            SelectionRectangle.Visibility = Visibility.Collapsed;
        }

        private void SetSelectionRectangle(Rect rect)
        {
            Canvas.SetLeft(SelectionRectangle, rect.X);
            Canvas.SetTop(SelectionRectangle, rect.Y);

            SelectionRectangle.Width = rect.Width;
            SelectionRectangle.Height = rect.Height;
            SelectionRectangle.Visibility = Visibility.Visible;
        }

        private WorldPointer _selectedPointer;

        private EditMode _editMode;
        private DrawMode _drawMode;

        private bool _isDragging = false;
        private Point _dragStartPoint;

        private void WorldRenderSource_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Point clickPoint = Snap(e.GetPosition(WorldRenderSource));

            WorldRenderSource.Focusable = true;
            WorldRenderSource.Focus();

            if (WorldEditorExitSelected != null)
            {
                WorldEditorExitSelected((int)clickPoint.X / 16, (int)clickPoint.Y / 16);
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (_world.Pointers.Where(o => o.BoundRectangle.Contains(clickPoint.X, clickPoint.Y)).FirstOrDefault() != null)
                {
                    SelectedEditMode.SelectedIndex = 1;
                }
                else
                {
                    SelectedEditMode.SelectedIndex = 0;
                }
            }

            switch (_editMode)
            {
                case EditMode.Tiles:
                    HandleTileClick(e);
                    break;

                case EditMode.Pointers:
                    HandlePointerClick(e);
                    break;
            }
        }

        private Point originalTilePoint;

        private void HandleTileClick(MouseButtonEventArgs e)
        {
            Point clickPoint = Snap(e.GetPosition(WorldRenderSource));

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                int x = (int)(clickPoint.X / 16), y = (int)(clickPoint.Y / 16);
                int tileValue = _worldDataAccessor.GetData(x, y);

                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    TileSelector.SelectedBlockValue = tileValue;
                    return;
                }

                _selectionMode = SelectionMode.SetTiles;

                if (_drawMode == DrawMode.Default)
                {
                    _dragStartPoint = clickPoint;
                    _isDragging = true;
                    originalTilePoint = clickPoint;
                }
                else if (_drawMode == DrawMode.Replace)
                {
                    if (tileValue != TileSelector.SelectedBlockValue)
                    {
                        _worldDataAccessor.ReplaceValue(tileValue, TileSelector.SelectedBlockValue);
                        Update();
                    }
                }
                else if (_drawMode == DrawMode.Fill)
                {
                    Stack<Point> stack = new Stack<Point>();
                    stack.Push(new Point(x, y));

                    int checkValue = _worldDataAccessor.GetData(x, y);
                    if (checkValue == TileSelector.SelectedBlockValue)
                    {
                        return;
                    }

                    int lowestX, highestX;
                    int lowestY, highestY;

                    lowestX = highestX = x;
                    lowestY = highestY = y;

                    while (stack.Count > 0)
                    {
                        Point point = stack.Pop();

                        int i = (int)(point.X);
                        int j = (int)(point.Y);

                        if (checkValue == _worldDataAccessor.GetData(i, j))
                        {
                            _worldDataAccessor.SetData(i, j, TileSelector.SelectedBlockValue);

                            if (i < lowestX)
                            {
                                lowestX = i;
                            }
                            if (i > highestX)
                            {
                                highestX = i;
                            }
                            if (j < lowestY)
                            {
                                lowestY = j;
                            }
                            if (j > highestY)
                            {
                                highestY = j;
                            }

                            stack.Push(new Point(i + 1, j));
                            stack.Push(new Point(i - 1, j));
                            stack.Push(new Point(i, j + 1));
                            stack.Push(new Point(i, j - 1));
                        }
                    }

                    TileChange tileChange = new TileChange(lowestX, lowestY, highestX - lowestX + 1, highestY - lowestY + 1);

                    for (int j = 0, row = lowestY; row <= highestY; row++, j++)
                    {
                        for (int i = 0, col = lowestX; col <= highestX; col++, i++)
                        {
                            tileChange.Data[i, j] = _worldDataAccessor.GetData(col, row) == TileSelector.SelectedBlockValue ? checkValue : -1;
                        }
                    }

                    _historyService.UndoTiles.Push(tileChange);

                    Update(new Rect(lowestX * 16, lowestY * 16, (highestX - lowestX + 1) * 16, (highestY - lowestY + 1) * 16));
                }
            }
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                _selectionMode = SelectionMode.SelectTiles;

                int x = (int)(clickPoint.X / 16), y = (int)(clickPoint.Y / 16);

                _dragStartPoint = clickPoint;
                _isDragging = true;
                originalTilePoint = clickPoint;

                SetSelectionRectangle(new Rect(x * 16, y * 16, 16, 16));
            }
        }

        private void HandlePointerClick(MouseButtonEventArgs e)
        {
            Point tilePoint = e.GetPosition(WorldRenderSource);
            List<Rect> updatedRects = new List<Rect>();

            if (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed)
            {
                if (e.RightButton == MouseButtonState.Pressed)
                {
                    if (_world.Pointers.Count < 10)
                    {
                        _selectedPointer = new WorldPointer()
                        {
                            X = (int)tilePoint.X / 16,
                            Y = (int)tilePoint.Y / 16
                        };

                        _world.Pointers.Add(_selectedPointer);
                        updatedRects.Add(_selectedPointer.BoundRectangle);
                    }
                }
                else
                {
                    _selectedPointer = _world.Pointers.Where(o => o.BoundRectangle.Contains(tilePoint.X, tilePoint.Y)).FirstOrDefault();
                }

                if (_selectedPointer != null)
                {
                    _dragStartPoint = tilePoint;
                    SetSelectionRectangle(_selectedPointer.BoundRectangle);
                    originalPointerPoint = new Point(_selectedPointer.X * 16, _selectedPointer.Y * 16);
                    _isDragging = true;

                    PointerEditor.Visibility = Visibility.Visible;
                    PointerEditor.SetPointer(_selectedPointer);
                }
                else
                {
                    PointerEditor.Visibility = Visibility.Collapsed;
                    ClearSelectionRectangle();
                }

                Update(updatedRects);
            }
        }

        private void WorldRenderSource_MouseMove(object sender, MouseEventArgs e)
        {
            Point tilePoint = Snap(e.GetPosition(WorldRenderSource));

            switch (_editMode)
            {
                case EditMode.Tiles:
                    HandleTileMove(e);
                    break;

                case EditMode.Pointers:
                    HandlePointerMove(e);
                    break;
            }
        }

        private SelectionMode _selectionMode;

        private void HandleTileMove(MouseEventArgs e)
        {
            Point tilePoint = Snap(e.GetPosition(WorldRenderSource));

            if (_isDragging && ((_selectionMode == SelectionMode.SetTiles && _drawMode == DrawMode.Default) ||
                               _selectionMode == SelectionMode.SelectTiles))
            {
                int x = (int)Math.Min(tilePoint.X, _dragStartPoint.X);
                int y = (int)Math.Min(tilePoint.Y, _dragStartPoint.Y);
                int width = (int)(Math.Max(tilePoint.X, _dragStartPoint.X)) - x;
                int height = (int)(Math.Max(tilePoint.Y, _dragStartPoint.Y)) - y;

                SetSelectionRectangle(new Rect(x, y, width + 16, height + 16));
            }
            else if (_selectionMode != SelectionMode.SelectTiles)
            {
                SetSelectionRectangle(new Rect(tilePoint.X, tilePoint.Y, 16, 16));
            }

            int blockX = (int)tilePoint.X / 16, blockY = (int)tilePoint.Y / 16;
            int tileValue = _worldDataAccessor.GetData(blockX, blockY);
            if (tileValue == -1)
            {
                InteractionDescription.Text = "";
                TerrainDescription.Text = "";
                PointerXY.Text = "";
                TileValue.Text = "";
            }
            else
            {
                TileBlock tileBlock = _tileSet.TileBlocks[tileValue];
                MapTileInteraction tileInteraction = _interactions.Where(t => t.Value == tileBlock.Property).FirstOrDefault();
                InteractionDescription.Text = tileInteraction.Name;
                PointerXY.Text = "X: " + blockX.ToString("X2") + " Y: " + blockY.ToString("X2");
                TileValue.Text = tileValue.ToString("X");
            }
        }

        private Point originalPointerPoint;

        private void HandlePointerMove(MouseEventArgs e)
        {
            Point movePoint = Snap(e.GetPosition(WorldRenderSource));

            if (_selectedPointer != null && _isDragging)
            {
                Point diffPoint = Snap(new Point(movePoint.X - originalPointerPoint.X, movePoint.Y - originalPointerPoint.Y));

                List<Rect> updateRects = new List<Rect>();

                Rect oldRect = _selectedPointer.BoundRectangle;

                if (oldRect.Right >= WorldRenderer.BITMAP_WIDTH)
                {
                    oldRect.Width = oldRect.Width - (oldRect.Right - WorldRenderer.BITMAP_WIDTH);
                }

                if (oldRect.Bottom >= WorldRenderer.BITMAP_HEIGHT)
                {
                    oldRect.Height = oldRect.Height - (oldRect.Bottom - WorldRenderer.BITMAP_HEIGHT);
                }

                updateRects.Add(oldRect);

                int newX = (int)((originalPointerPoint.X + diffPoint.X) / 16);
                int newY = (int)((originalPointerPoint.Y + diffPoint.Y) / 16);

                if (newX == _selectedPointer.X && newY == _selectedPointer.Y)
                {
                    return;
                }

                _selectedPointer.X = newX;
                _selectedPointer.Y = newY;

                Rect updateArea = _selectedPointer.BoundRectangle;

                if (updateArea.Right >= WorldRenderer.BITMAP_WIDTH)
                {
                    updateArea.Width = updateArea.Width - (updateArea.Right - WorldRenderer.BITMAP_WIDTH);
                }

                if (updateArea.Bottom >= WorldRenderer.BITMAP_HEIGHT)
                {
                    updateArea.Height = updateArea.Height - (updateArea.Bottom - WorldRenderer.BITMAP_HEIGHT);
                }

                updateRects.Add(updateArea);

                SetSelectionRectangle(_selectedPointer.BoundRectangle);
                Update(updateRects);
            }

            int blockX = (int)movePoint.X / 16, blockY = (int)movePoint.Y / 16;
            PointerXY.Text = "X: " + blockX.ToString("X2") + " Y: " + blockY.ToString("X2");
        }

        private int Snap(double value)
        {
            return (int)(Math.Floor(value / 16) * 16);
        }

        private Point Snap(Point value)
        {
            return new Point(Snap(value.X), Snap(value.Y));
        }

        private int[,] _copyBuffer;

        private void CutSelection()
        {
            if (_selectionMode == SelectionMode.SelectTiles)
            {
                _copyBuffer = new int[(int)(SelectionRectangle.Width / 16), (int)(SelectionRectangle.Height / 16)];

                int startX = (int)(Canvas.GetLeft(SelectionRectangle) / 16);
                int endX = (int)(SelectionRectangle.Width / 16) + startX;
                int startY = (int)(Canvas.GetTop(SelectionRectangle) / 16);
                int endY = (int)(SelectionRectangle.Height / 16) + startY;
                for (int r = startY, y = 0; r < endY; r++, y++)
                {
                    for (int c = startX, x = 0; c < endX; c++, x++)
                    {
                        _copyBuffer[x, y] = _worldDataAccessor.GetData(c, r);
                        _worldDataAccessor.SetData(c, r, 0x41);
                    }
                }

                Update(new Rect(Canvas.GetLeft(SelectionRectangle),
                                Canvas.GetTop(SelectionRectangle),
                                SelectionRectangle.Width,
                                SelectionRectangle.Height));

                SetSelectionRectangle(new Rect((endX - 1) * 16, (endY - 1) * 16, 16, 16));

                _selectionMode = SelectionMode.SetTiles;
            }
        }

        private void CopySelection()
        {
            if (_selectionMode == SelectionMode.SelectTiles)
            {
                _copyBuffer = new int[(int)(SelectionRectangle.Width / 16), (int)(SelectionRectangle.Height / 16)];

                int startX = (int)(Canvas.GetLeft(SelectionRectangle) / 16);
                int endX = (int)(SelectionRectangle.Width / 16) + startX;
                int startY = (int)(Canvas.GetTop(SelectionRectangle) / 16);
                int endY = (int)(SelectionRectangle.Height / 16) + startY;
                for (int r = startY, y = 0; r < endY; r++, y++)
                {
                    for (int c = startX, x = 0; c < endX; c++, x++)
                    {
                        _copyBuffer[x, y] = _worldDataAccessor.GetData(c, r);
                    }
                }

                SetSelectionRectangle(new Rect((endX - 1) * 16, (endY - 1) * 16, 16, 16));

                _selectionMode = SelectionMode.SetTiles;
            }
        }

        private void PasteSelection()
        {
            if (_copyBuffer != null)
            {
                int startX = (int)(Canvas.GetLeft(SelectionRectangle) / 16);
                int endX = (int)(SelectionRectangle.Width / 16) + startX;
                int startY = (int)(Canvas.GetTop(SelectionRectangle) / 16);
                int endY = (int)(SelectionRectangle.Height / 16) + startY;
                int bufferWidth = _copyBuffer.GetLength(0);
                int bufferHeight = _copyBuffer.GetLength(1);

                if (startX + bufferWidth > World.BLOCK_WIDTH)
                {
                    bufferWidth = World.BLOCK_WIDTH - startX;
                }

                if (startY + bufferHeight > World.BLOCK_HEIGHT)
                {
                    bufferHeight = World.BLOCK_HEIGHT - startY;
                }

                if (SelectionRectangle.Width == 16 &&
                    SelectionRectangle.Height == 16)
                {
                    for (int r = startY, y = 0; y < bufferHeight; r++, y++)
                    {
                        for (int c = startX, x = 0; x < bufferWidth; c++, x++)
                        {
                            _worldDataAccessor.SetData(c, r, _copyBuffer[x % bufferWidth, y % bufferHeight]);
                        }
                    }

                    Update(new Rect(startX * 16, startY * 16, bufferWidth * 16, bufferHeight * 16));
                }
                else
                {
                    for (int r = startY, y = 0; r < endY; r++, y++)
                    {
                        for (int c = startX, x = 0; c < endX; c++, x++)
                        {
                            _worldDataAccessor.SetData(c, r, _copyBuffer[x % bufferWidth, y % bufferHeight]);
                        }
                    }

                    Update(new Rect(Canvas.GetLeft(SelectionRectangle),
                                    Canvas.GetTop(SelectionRectangle),
                                    SelectionRectangle.Width,
                                    SelectionRectangle.Height));
                }

                SetSelectionRectangle(new Rect((endX - 1) * 16, (endY - 1) * 16, 16, 16));

                _selectionMode = SelectionMode.SetTiles;
            }
        }

        private void UndoTiles()
        {
            if (_historyService.UndoTiles.Count > 0)
            {
                TileChange undoTiles = _historyService.UndoTiles.Pop();
                _historyService.RedoTiles.Push(ApplyTileChange(undoTiles));
            }
        }

        private void RedoTiles()
        {
            if (_historyService.RedoTiles.Count > 0)
            {
                TileChange redoTiles = _historyService.RedoTiles.Pop();
                _historyService.UndoTiles.Push(ApplyTileChange(redoTiles));
            }
        }

        private void DeletePointer()
        {
            if (_selectedPointer != null)
            {
                _world.Pointers.Remove(_selectedPointer);
                Update(_selectedPointer.BoundRectangle);
                ClearSelectionRectangle();
                PointerEditor.Visibility = Visibility.Collapsed;
            }
        }

        private void WorldRenderSource_MouseUp(object sender, MouseButtonEventArgs e)
        {
            switch (_editMode)
            {
                case EditMode.Tiles:
                    HandleTileRelease(e);
                    break;

                case EditMode.Pointers:
                    HandlePointerRelease(e);
                    break;
            }
        }

        private TileChange ApplyTileChange(TileChange tileChange)
        {
            int columnStart = tileChange.X;
            int rowStart = tileChange.Y;
            int columnEnd = tileChange.X + tileChange.Width;
            int rowEnd = tileChange.Y + tileChange.Height;

            TileChange reverseTileChange = new TileChange(columnStart, rowStart, columnEnd - columnStart, rowEnd - rowStart);
            for (int c = columnStart, i = 0; c < columnEnd; c++, i++)
            {
                for (int r = rowStart, j = 0; r < rowEnd; r++, j++)
                {
                    if (tileChange.Data[i, j] > -1)
                    {
                        reverseTileChange.Data[i, j] = _worldDataAccessor.GetData(c, r);
                        _worldDataAccessor.SetData(c, r, tileChange.Data[i, j]);
                    }
                }
            }

            Update(new Rect(columnStart * 16, rowStart * 16, (columnEnd - columnStart) * 16, (rowEnd - rowStart) * 16));
            return reverseTileChange;
        }

        private void HandleTileRelease(MouseButtonEventArgs e)
        {
            Point mousePoint = Snap(e.GetPosition(WorldRenderSource));

            if (_drawMode == DrawMode.Default && _isDragging)
            {
                _isDragging = false;

                if (_selectionMode == SelectionMode.SetTiles)
                {
                    int columnStart = (int)(Math.Min(originalTilePoint.X, mousePoint.X) / 16);
                    int rowStart = (int)(Math.Min(originalTilePoint.Y, mousePoint.Y) / 16);
                    int columnEnd = (int)(Math.Max(originalTilePoint.X, mousePoint.X) / 16);
                    int rowEnd = (int)(Math.Max(originalTilePoint.Y, mousePoint.Y) / 16);

                    TileChange tileChange = new TileChange(columnStart, rowStart, (columnEnd - columnStart) + 1, (rowEnd - rowStart) + 1);

                    for (int c = columnStart, i = 0; c <= columnEnd; c++, i++)
                    {
                        for (int r = rowStart, j = 0; r <= rowEnd; r++, j++)
                        {
                            tileChange.Data[i, j] = _worldDataAccessor.GetData(c, r);
                            _worldDataAccessor.SetData(c, r, TileSelector.SelectedBlockValue);
                        }
                    }

                    _historyService.UndoTiles.Push(tileChange);
                    _historyService.RedoTiles.Clear();

                    Update(new Rect(columnStart * 16, rowStart * 16, (columnEnd - columnStart + 1) * 16, (rowEnd - rowStart + 1) * 16));
                    SetSelectionRectangle(new Rect(mousePoint.X, mousePoint.Y, 16, 16));
                }
                else if (_selectionMode == SelectionMode.SelectTiles)
                {
                    if (SelectionRectangle.Width == 16 && SelectionRectangle.Height == 16)
                    {
                        _selectionMode = SelectionMode.SetTiles;
                    }
                }
            }
        }

        private void HandlePointerRelease(MouseButtonEventArgs e)
        {
            if (_selectedPointer != null && _isDragging)
            {
                PointerEditor.Visibility = Visibility.Visible;
            }

            _isDragging = false;
        }

        private void UpdateTextTables()
        {
            List<KeyValuePair<string, string>> _graphicsSetNames = new List<KeyValuePair<string, string>>();
            for (int i = 0; i < 256; i++)
            {
                _graphicsSetNames.Add(new KeyValuePair<string, string>(i.ToString(), i.ToString("X")));
            }

            foreach (var kv in _textService.GetTable("graphics"))
            {
                _graphicsSetNames[int.Parse(kv.Key, System.Globalization.NumberStyles.HexNumber)] = new KeyValuePair<string, string>(kv.Key, kv.Value);
            }

            Music.ItemsSource = _textService.GetTable("music").OrderBy(kv => kv.Value);
            PaletteIndex.ItemsSource = _palettesService.GetPalettes();
            GraphicsSet.ItemsSource = _graphicsSetNames;
            Screens.ItemsSource = new int[4] { 1, 2, 3, 4 };

            Music.SelectedValue = _world.MusicValue.ToString("X");
            PaletteIndex.SelectedValue = _world.PaletteId;
            GraphicsSet.SelectedValue = _world.TileTableIndex.ToString("X");
            Screens.SelectedIndex = _world.ScreenLength - 1;
        }

        private void GraphicsSet_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _world.TileTableIndex = int.Parse(GraphicsSet.SelectedValue.ToString(), System.Globalization.NumberStyles.HexNumber);
            _graphicsAccessor.SetBottomTable(_graphicsService.GetTileSection(_world.TileTableIndex));
            TileSelector.Update();
            Update();
        }

        private void Music_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _world.MusicValue = int.Parse(Music.SelectedValue.ToString(), System.Globalization.NumberStyles.HexNumber);
        }

        private void PaletteIndex_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PaletteIndex.SelectedItem != null)
            {
                _world.PaletteId = ((Palette)PaletteIndex.SelectedItem).Id;

                Palette palette = _palettesService.GetPalette(_world.PaletteId);
                _worldRenderer.Update(palette: palette);
                TileSelector.Update(palette: palette);
                Update();
            }
        }

        private void Screens_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _world.ScreenLength = int.Parse(Screens.SelectedValue.ToString());
            CanvasContainer.Width = RenderContainer.Width = _world.ScreenLength * 16 * 16;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveWorld();
        }

        private void SaveWorld()
        {
            _world.CompressedData = _compressionService.CompressWorld(_world);
            _worldService.SaveWorld(_world);

            AlertWindow.Alert(_world.Name + " has been saved!");
        }

        private void TileSelector_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount >= 2)
            {
                GlobalPanels.EditTileBlock(_world.Id, TileSelector.SelectedBlockValue);
            }
        }

        private void WorldRenderSource_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.S:
                        SaveWorld();
                        break;

                    case Key.C:
                        if (_editMode == EditMode.Tiles)
                        {
                            CopySelection();
                        }
                        break;

                    case Key.V:
                        if (_editMode == EditMode.Tiles)
                        {
                            PasteSelection();
                        }
                        break;

                    case Key.X:
                        if (_editMode == EditMode.Tiles)
                        {
                            CutSelection();
                        }
                        break;

                    case Key.Z:
                        if (_editMode == EditMode.Tiles)
                        {
                            UndoTiles();
                        }
                        break;

                    case Key.Y:
                        if (_editMode == EditMode.Tiles)
                        {
                            RedoTiles();
                        }
                        break;
                }
            }

            switch (e.Key)
            {
                case Key.Delete:
                    if (_editMode == EditMode.Pointers)
                    {
                        DeletePointer();
                    }
                    break;
            }
        }

        private void WorldRenderSource_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (!PointerEditor.IsMouseOver && WorldScroller.IsMouseOver)
            {
                WorldRenderSource.Focus();
            }
        }

        private void DrawMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (SelectedDrawMode.SelectedIndex)
            {
                case 0:
                    _drawMode = DrawMode.Default;
                    break;

                case 1:
                    _drawMode = DrawMode.Fill;
                    break;

                case 2:
                    _drawMode = DrawMode.Replace;
                    break;
            }
        }

        private void SelectedEditMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SelectionRectangle != null) SelectionRectangle.Visibility = Visibility.Collapsed;

            switch (SelectedEditMode.SelectedIndex)
            {
                case 0:
                    _editMode = EditMode.Tiles;
                    PointerEditor.Visibility = Visibility.Collapsed;
                    break;

                case 1:
                    _editMode = EditMode.Pointers;
                    break;
            }
        }

        private void ShowPSwitch_Checked(object sender, RoutedEventArgs e)
        {
            Update();
        }

        private void ShowInteraction_Click(object sender, RoutedEventArgs e)
        {
            TileSelector.Update(withMapInteractionOverlay: ShowInteraction.IsChecked.Value);
            Update();
        }

        private void ShowGrid_Click(object sender, RoutedEventArgs e)
        {
            _worldRenderer.RenderGrid = ShowGrid.IsChecked.Value;
            Update();
        }
    }
}