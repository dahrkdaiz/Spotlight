﻿using Spotlight.Models;
using Spotlight.Renderers;
using Spotlight.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Newtonsoft.Json;

namespace Spotlight
{
    /// <summary>
    /// Interaction logic for PalettePanel.xaml
    /// </summary>
    public partial class PaletteEditor : UserControl
    {
        public PaletteEditor(PalettesService palettesService)
        {
            InitializeComponent();

            _palettesService = palettesService;

            _bitmapSection = new WriteableBitmap(256, 32, 96, 96, PixelFormats.Bgra32, null);
            _bitmapFull = new WriteableBitmap(256, 64, 96, 96, PixelFormats.Bgra32, null);

            ImageSection.Source = _bitmapSection;
            ImageFull.Source = _bitmapFull;

            _rendererSection = new PaletteRenderer(_palettesService, PaletteType.Section);
            _rendererFull = new PaletteRenderer(_palettesService, PaletteType.Full);

            PaletteList.ItemsSource = _palettesService.GetPalettes();
            PaletteList.SelectedIndex = 0;

            UpdateFull();
            _palettesService.PalettesChanged += _palettesService_PalettesChanged;
        }

        private void _palettesService_PalettesChanged()
        {
            PaletteList.ItemsSource = _palettesService.GetPalettes();
        }

        private WriteableBitmap _bitmapSection;
        private WriteableBitmap _bitmapFull;

        private PaletteRenderer _rendererSection;
        private PaletteRenderer _rendererFull;

        private PalettesService _palettesService;

        private void Update()
        {
            if (_bitmapSection == null || _rendererSection == null)
            {
                return;
            }

            _palettesService.CacheRgbPalettes(_editingPalette);
            _rendererSection.Update();
            _bitmapSection.Lock();

            Int32Rect sourceArea = new Int32Rect(0, 0, 256, 32);
            _bitmapSection.WritePixels(sourceArea, _rendererSection.GetRectangle(sourceArea), sourceArea.Width * 4, sourceArea.X, sourceArea.Y);
            _bitmapSection.AddDirtyRect(sourceArea);
            _bitmapSection.Unlock();
        }

        private void UpdateFull()
        {
            if (_bitmapFull == null || _rendererFull == null)
            {
                return;
            }

            _rendererFull.Update();
            _bitmapFull.Lock();

            Int32Rect sourceArea = new Int32Rect(0, 0, 256, 64);
            _bitmapFull.WritePixels(sourceArea, _rendererFull.GetRectangle(sourceArea), sourceArea.Width * 4, sourceArea.X, sourceArea.Y);
            _bitmapFull.AddDirtyRect(sourceArea);
            _bitmapFull.Unlock();
        }

        private Point Snap(Point value)
        {
            return new Point(Snap(value.X), Snap(value.Y));
        }

        private int Snap(double value)
        {
            return (int)(Math.Floor(value / 16) * 16);
        }

        private int paletteSectionIndex;
        private void PaletteSection_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Point sectionPoint = Snap(e.GetPosition(ImageSection));
            paletteSectionIndex = (int)(sectionPoint.Y + (sectionPoint.X / 16));
            Canvas.SetTop(SectionSelectionRectangle, sectionPoint.Y);
            Canvas.SetLeft(SectionSelectionRectangle, sectionPoint.X);

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (paletteSectionIndex % 4 == 0)
                {
                    _editingPalette.IndexedColors[0] = paletteFullIndex;
                    _editingPalette.IndexedColors[4] = paletteFullIndex;
                    _editingPalette.IndexedColors[8] = paletteFullIndex;
                    _editingPalette.IndexedColors[12] = paletteFullIndex;
                    _editingPalette.IndexedColors[16] = paletteFullIndex;
                    _editingPalette.IndexedColors[20] = paletteFullIndex;
                    _editingPalette.IndexedColors[24] = paletteFullIndex;
                    _editingPalette.IndexedColors[28] = paletteFullIndex;
                }
                else
                {
                    _editingPalette.IndexedColors[paletteSectionIndex] = paletteFullIndex;
                }

                SectionTextValue.Text = paletteFullIndex.ToString("X2");
                Update();
            }
            else if(e.RightButton == MouseButtonState.Pressed)
            {
                paletteFullIndex = _editingPalette.IndexedColors[paletteSectionIndex];
                Canvas.SetTop(FullSelectionRectangle, ((paletteFullIndex / 16) * 16));
                Canvas.SetLeft(FullSelectionRectangle, ((paletteFullIndex % 16) * 16));
                FullTextValue.Text = paletteFullIndex.ToString("X2");
            }
        }
        
        private int paletteFullIndex;
        private void FullSection_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Point sectionPoint = Snap(e.GetPosition(ImageFull));
            paletteFullIndex = (int)(sectionPoint.Y + (sectionPoint.X / 16));
            Canvas.SetTop(FullSelectionRectangle, sectionPoint.Y);
            Canvas.SetLeft(FullSelectionRectangle, sectionPoint.X);
        }

        private void ImageSection_MouseMove(object sender, MouseEventArgs e)
        {
            Point sectionPoint = Snap(e.GetPosition(ImageSection));
            SectionTextValue.Text = ((int)(sectionPoint.Y + (sectionPoint.X / 16))).ToString("X2");
        }

        private void ImageFull_MouseMove(object sender, MouseEventArgs e)
        {
            Point sectionPoint = Snap(e.GetPosition(ImageFull));
            FullTextValue.Text = ((int)(sectionPoint.Y + (sectionPoint.X / 16))).ToString("X2");
        }

        private Palette _editingPalette;
        private void PaletteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _editingPalette = JsonConvert.DeserializeObject<Palette>(JsonConvert.SerializeObject(PaletteList.SelectedItem));
            if (_editingPalette != null)
            {
                _rendererSection.Update(_editingPalette);
                Update();
            }
        }

        private void ApplyPalette_Click(object sender, RoutedEventArgs e)
        {
            _palettesService.CommitPalette(_editingPalette);
        }

        private void ResetPalette_Click(object sender, RoutedEventArgs e)
        {
            Palette commitPallete = (Palette)PaletteList.SelectedItem;

            for (int i = 0; i < 32; i++)
            {
                _editingPalette.IndexedColors[i] = commitPallete.IndexedColors[i];
            }

            Update();
        }

        private void RemovePalette_Click(object sender, RoutedEventArgs e)
        {
            if(ConfirmationWindow.Confirm("Are you sure you would like to remove this palette?") == System.Windows.Forms.DialogResult.Yes)
            {
                _palettesService.RemovePalette(_editingPalette);
            }
        }

        private void AddPalette_Click(object sender, RoutedEventArgs e)
        {
            string paletteName = InputWindow.GetInput("Enter palette name.");
            if(paletteName != null)
            {
                PaletteList.SelectedValue = _palettesService.NewPalette(paletteName, _editingPalette).Id;
            }
        }
    }
}
