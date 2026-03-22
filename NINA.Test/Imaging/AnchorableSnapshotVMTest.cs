#region "copyright"

/*
    Copyright © 2016 - 2026 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using FluentAssertions;
using Moq;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.Image.ImageData;
using NINA.Image.Interfaces;
using NINA.Profile.Interfaces;
using NINA.ViewModel.Imaging;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using Nito.AsyncEx;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace NINA.Test.Imaging {

    [TestFixture]
    internal class AnchorableSnapshotVMTest {
        private const int TestImageId = 42;

        private static readonly IEnumerable<string> ImageTypes = new[] {
            CaptureSequence.ImageTypes.LIGHT,
            CaptureSequence.ImageTypes.FLAT,
            CaptureSequence.ImageTypes.DARK,
            CaptureSequence.ImageTypes.BIAS,
            CaptureSequence.ImageTypes.SNAPSHOT
        };

        private Mock<IApplicationStatusMediator> applicationStatusMediatorMock;
        private Mock<ICameraMediator> cameraMediatorMock;
        private Mock<IFilterWheelMediator> filterWheelMediatorMock;
        private Mock<IImageHistoryVM> historyMock;
        private Mock<IImageSaveMediator> imageSaveMediatorMock;
        private Mock<IImagingMediator> imagingMediatorMock;
        private Mock<IProfileService> profileServiceMock;

        [SetUp]
        [Apartment(ApartmentState.STA)]
        public void Setup() {
            EnsureApplicationResources();

            applicationStatusMediatorMock = new Mock<IApplicationStatusMediator>();
            cameraMediatorMock = new Mock<ICameraMediator>();
            filterWheelMediatorMock = new Mock<IFilterWheelMediator>();
            historyMock = new Mock<IImageHistoryVM>();
            imageSaveMediatorMock = new Mock<IImageSaveMediator>();
            imagingMediatorMock = new Mock<IImagingMediator>();
            profileServiceMock = new Mock<IProfileService>();

            cameraMediatorMock.Setup(x => x.IsFreeToCapture(It.IsAny<object>())).Returns(true);
        }

        [Test]
        [Apartment(ApartmentState.STA)]
        [TestCaseSource(nameof(ImageTypes))]
        public async Task SnapImage_UsesSelectedImageType_ForAllImageTypes(string imageType) {
            profileServiceMock.SetupGet(x => x.ActiveProfile).Returns(CreateProfile(imageType, save: true));
            SetupSuccessfulSnapImageFlow();

            var sut = CreateSut();

            var result = await sut.SnapImage(new Progress<ApplicationStatus>());

            result.Should().BeTrue();
            imagingMediatorMock.Verify(
                x => x.CaptureImage(
                    It.Is<CaptureSequence>(cs => cs.ImageType == imageType),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<IProgress<ApplicationStatus>>(),
                    It.IsAny<string>()),
                Times.Once);
            historyMock.Verify(x => x.Add(TestImageId, It.IsAny<IImageStatistics>(), imageType), Times.Once);
        }

        [Test]
        [Apartment(ApartmentState.STA)]
        [TestCaseSource(nameof(ImageTypes))]
        public async Task StartLiveViewCommand_UsesSelectedImageType_ForAllImageTypes(string imageType) {
            profileServiceMock.SetupGet(x => x.ActiveProfile).Returns(CreateProfile(imageType));
            imagingMediatorMock.Setup(x => x.StartLiveView(It.IsAny<CaptureSequence>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var sut = CreateSut();

            await sut.StartLiveViewCommand.ExecuteAsync(null);

            imagingMediatorMock.Verify(
                x => x.StartLiveView(
                    It.Is<CaptureSequence>(cs => cs.ImageType == imageType),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        private void SetupSuccessfulSnapImageFlow() {
            var imageMock = new Mock<IExposureData>();
            var imageDataMock = new Mock<IImageData>();
            var statisticsMock = new Mock<IImageStatistics>();
            var renderedImageMock = new Mock<IRenderedImage>();

            imageDataMock.SetupGet(x => x.Statistics).Returns(new AsyncLazy<IImageStatistics>(() => Task.FromResult(statisticsMock.Object)));
            imageDataMock.SetupGet(x => x.MetaData).Returns(new ImageMetaData() { Image = new ImageParameter { Id = TestImageId } });
            imageMock.Setup(x => x.ToImageData(It.IsAny<IProgress<ApplicationStatus>>(), It.IsAny<CancellationToken>())).ReturnsAsync(imageDataMock.Object);

            imagingMediatorMock.Setup(x => x.CaptureImage(It.IsAny<CaptureSequence>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<ApplicationStatus>>(), It.IsAny<string>())).ReturnsAsync(imageMock.Object);
            imagingMediatorMock.Setup(x => x.PrepareImage(It.IsAny<IImageData>(), It.IsAny<PrepareImageParameters>(), It.IsAny<CancellationToken>())).ReturnsAsync(renderedImageMock.Object);
            imageSaveMediatorMock.Setup(x => x.Enqueue(It.IsAny<IImageData>(), It.IsAny<Task<IRenderedImage>>(), It.IsAny<IProgress<ApplicationStatus>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        }

        private static NINA.Profile.Profile CreateProfile(string imageType, bool save = false) {
            var profile = new NINA.Profile.Profile();
            profile.SnapShotControlSettings.ImageType = imageType;
            profile.SnapShotControlSettings.Save = save;
            return profile;
        }

        private AnchorableSnapshotVM CreateSut() {
            return new AnchorableSnapshotVM(
                profileServiceMock.Object,
                imagingMediatorMock.Object,
                cameraMediatorMock.Object,
                applicationStatusMediatorMock.Object,
                imageSaveMediatorMock.Object,
                historyMock.Object,
                filterWheelMediatorMock.Object);
        }

        private static void EnsureApplicationResources() {
            if (Application.Current == null) {
                new Application();
            }

            if (!Application.Current.Resources.Contains("PuzzlePieceSVG")) {
                Application.Current.Resources["PuzzlePieceSVG"] = new GeometryGroup();
            }

            if (!Application.Current.Resources.Contains("ImagingSVG")) {
                Application.Current.Resources["ImagingSVG"] = new GeometryGroup();
            }
        }
    }
}
