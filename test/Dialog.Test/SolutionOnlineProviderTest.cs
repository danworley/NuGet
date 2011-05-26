﻿using System;
using System.Collections.Generic;
using System.Threading;
using EnvDTE;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NuGet.Dialog.PackageManagerUI;
using NuGet.Dialog.Providers;
using NuGet.Test;
using NuGet.Test.Mocks;
using NuGet.VisualStudio;

namespace NuGet.Dialog.Test {
    [TestClass]
    public class SolutionOnlineProviderTest {

        [TestMethod]
        public void ExecuteMethodCallsInstallPackageMethodOnPackageManager() {
            // Arrange
            var packageA = PackageUtility.CreatePackage("A", "1.0");
            var packageB = PackageUtility.CreatePackage("B", "2.0");
            var packageC = PackageUtility.CreatePackage("C", "3.0");

            var sourceRepository = new MockPackageRepository();
            sourceRepository.AddPackage(packageA);
            sourceRepository.AddPackage(packageC);
            sourceRepository.AddPackage(packageB);

            var localRepository = new MockPackageRepository();
            localRepository.AddPackage(packageA);

            var projectManager1 = new Mock<IProjectManager>();
            projectManager1.Setup(p => p.LocalRepository).Returns(localRepository);

            var projectManager2 = new Mock<IProjectManager>();
            projectManager2.Setup(p => p.LocalRepository).Returns(localRepository);

            var project1 = MockProjectUtility.CreateMockProject("Project1");
            var project2 = MockProjectUtility.CreateMockProject("Project2");

            var packageManager = new Mock<IVsPackageManager>();
            packageManager.Setup(p => p.SourceRepository).Returns(sourceRepository);
            packageManager.Setup(p => p.GetProjectManager(It.Is<Project>(s => s == project1))).Returns(projectManager1.Object);
            packageManager.Setup(p => p.GetProjectManager(It.Is<Project>(s => s == project2))).Returns(projectManager2.Object);
            packageManager.Setup(p => p.IsProjectLevel(It.IsAny<IPackage>())).Returns(true);

            var solutionManager = new Mock<ISolutionManager>();
            solutionManager.Setup(p => p.GetProject(It.Is<string>(s => s == "Project1"))).Returns(project1);
            solutionManager.Setup(p => p.GetProject(It.Is<string>(s => s == "Project2"))).Returns(project2);
            solutionManager.Setup(p => p.GetProjects()).Returns(new Project[] { project1, project2 });

            var provider = CreateSolutionOnlineProvider(packageManager.Object, localRepository, solutionManager: solutionManager.Object);
            var extensionTree = provider.ExtensionsTree;

            var firstTreeNode = (SimpleTreeNode)extensionTree.Nodes[0];
            firstTreeNode.Repository.AddPackage(packageA);
            firstTreeNode.Repository.AddPackage(packageB);
            firstTreeNode.Repository.AddPackage(packageC);

            provider.SelectedNode = firstTreeNode;
            IVsPackageManager activePackageManager = provider.GetActivePackageManager();
            Mock<IVsPackageManager> mockPackageManager = Mock.Get<IVsPackageManager>(activePackageManager);

            var manualEvent = new ManualResetEventSlim(false);

            provider.ExecuteCompletedCallback = delegate {
                // Assert
                mockPackageManager.Verify(p => p.InstallPackage(
                    new Project[] { project1, project2 },
                    packageB,
                    It.IsAny<IEnumerable<PackageOperation>>(),
                    false,
                    provider,
                    provider), Times.Once());

                manualEvent.Set();
            };

            var extensionB = new PackageItem(provider, packageB);

            // Act
            provider.Execute(extensionB);

            // do not allow the method to return
            manualEvent.Wait();
        }

        [TestMethod]
        public void ExecuteMethodDoNotCallInstallPackageIfUserPressCancelOnTheProjectSelectorButton() {
            // Arrange
            var packageA = PackageUtility.CreatePackage("A", "1.0");
            var packageB = PackageUtility.CreatePackage("B", "2.0");
            var packageC = PackageUtility.CreatePackage("C", "3.0");

            var sourceRepository = new MockPackageRepository();
            sourceRepository.AddPackage(packageA);
            sourceRepository.AddPackage(packageC);
            sourceRepository.AddPackage(packageB);

            var localRepository = new MockPackageRepository();
            localRepository.AddPackage(packageA);

            var projectManager1 = new Mock<IProjectManager>();
            projectManager1.Setup(p => p.LocalRepository).Returns(localRepository);

            var projectManager2 = new Mock<IProjectManager>();
            projectManager2.Setup(p => p.LocalRepository).Returns(localRepository);

            var project1 = MockProjectUtility.CreateMockProject("Project1");
            var project2 = MockProjectUtility.CreateMockProject("Project2");

            var packageManager = new Mock<IVsPackageManager>();
            packageManager.Setup(p => p.SourceRepository).Returns(sourceRepository);
            packageManager.Setup(p => p.GetProjectManager(It.Is<Project>(s => s == project1))).Returns(projectManager1.Object);
            packageManager.Setup(p => p.GetProjectManager(It.Is<Project>(s => s == project2))).Returns(projectManager2.Object);
            packageManager.Setup(p => p.IsProjectLevel(It.IsAny<IPackage>())).Returns(true);

            var solutionManager = new Mock<ISolutionManager>();
            solutionManager.Setup(p => p.GetProject(It.Is<string>(s => s == "Project1"))).Returns(project1);
            solutionManager.Setup(p => p.GetProject(It.Is<string>(s => s == "Project2"))).Returns(project2);
            solutionManager.Setup(p => p.GetProjects()).Returns(new Project[] { project1, project2 });

            var mockProjectSelector = new Mock<IProjectSelectorService>();
            mockProjectSelector.Setup(p => p.ShowProjectSelectorWindow(It.IsAny<Func<Project, bool>>(), It.IsAny<Func<Project, bool>>())).Returns((Func<IEnumerable<Project>>)null);

            var provider = CreateSolutionOnlineProvider(packageManager.Object, localRepository, solutionManager: solutionManager.Object, projectSelectorService: mockProjectSelector.Object);
            var extensionTree = provider.ExtensionsTree;

            var firstTreeNode = (SimpleTreeNode)extensionTree.Nodes[0];
            firstTreeNode.Repository.AddPackage(packageA);
            firstTreeNode.Repository.AddPackage(packageB);
            firstTreeNode.Repository.AddPackage(packageC);

            provider.SelectedNode = firstTreeNode;
            IVsPackageManager activePackageManager = provider.GetActivePackageManager();
            Mock<IVsPackageManager> mockPackageManager = Mock.Get<IVsPackageManager>(activePackageManager);

            var manualEvent = new ManualResetEventSlim(false);

            provider.ExecuteCompletedCallback = delegate {
                // Assert
                mockPackageManager.Verify(p => p.InstallPackage(
                    It.IsAny<IEnumerable<Project>>(),
                    packageB,
                    It.IsAny<IEnumerable<PackageOperation>>(),
                    false,
                    provider,
                    provider), Times.Never());

                manualEvent.Set();
            };

            var extensionB = new PackageItem(provider, packageB);

            // Act
            provider.Execute(extensionB);

            // do not allow the method to return
            manualEvent.Wait();
        }

        [TestMethod]
        public void ExecuteMethodDoNotCallInstallPackageIfUserDoesNotSelectAnyProject() {
            // Arrange
            var packageA = PackageUtility.CreatePackage("A", "1.0");
            var packageB = PackageUtility.CreatePackage("B", "2.0");
            var packageC = PackageUtility.CreatePackage("C", "3.0");

            var sourceRepository = new MockPackageRepository();
            sourceRepository.AddPackage(packageA);
            sourceRepository.AddPackage(packageC);
            sourceRepository.AddPackage(packageB);

            var localRepository = new MockPackageRepository();
            localRepository.AddPackage(packageA);

            var projectManager1 = new Mock<IProjectManager>();
            projectManager1.Setup(p => p.LocalRepository).Returns(localRepository);

            var projectManager2 = new Mock<IProjectManager>();
            projectManager2.Setup(p => p.LocalRepository).Returns(localRepository);

            var project1 = MockProjectUtility.CreateMockProject("Project1");
            var project2 = MockProjectUtility.CreateMockProject("Project2");

            var packageManager = new Mock<IVsPackageManager>();
            packageManager.Setup(p => p.SourceRepository).Returns(sourceRepository);
            packageManager.Setup(p => p.GetProjectManager(It.Is<Project>(s => s == project1))).Returns(projectManager1.Object);
            packageManager.Setup(p => p.GetProjectManager(It.Is<Project>(s => s == project2))).Returns(projectManager2.Object);
            packageManager.Setup(p => p.IsProjectLevel(It.IsAny<IPackage>())).Returns(true);

            var solutionManager = new Mock<ISolutionManager>();
            solutionManager.Setup(p => p.GetProject(It.Is<string>(s => s == "Project1"))).Returns(project1);
            solutionManager.Setup(p => p.GetProject(It.Is<string>(s => s == "Project2"))).Returns(project2);
            solutionManager.Setup(p => p.GetProjects()).Returns(new Project[] { project1, project2 });

            var mockProjectSelector = new Mock<IProjectSelectorService>();
            mockProjectSelector.Setup(p => p.ShowProjectSelectorWindow(It.IsAny<Func<Project, bool>>(), It.IsAny<Func<Project, bool>>())).Returns(new Project[0]);

            var provider = CreateSolutionOnlineProvider(packageManager.Object, localRepository, solutionManager: solutionManager.Object, projectSelectorService: mockProjectSelector.Object);
            var extensionTree = provider.ExtensionsTree;

            var firstTreeNode = (SimpleTreeNode)extensionTree.Nodes[0];
            firstTreeNode.Repository.AddPackage(packageA);
            firstTreeNode.Repository.AddPackage(packageB);
            firstTreeNode.Repository.AddPackage(packageC);

            provider.SelectedNode = firstTreeNode;
            IVsPackageManager activePackageManager = provider.GetActivePackageManager();
            Mock<IVsPackageManager> mockPackageManager = Mock.Get<IVsPackageManager>(activePackageManager);

            var manualEvent = new ManualResetEventSlim(false);

            provider.ExecuteCompletedCallback = delegate {
                // Assert
                mockPackageManager.Verify(p => p.InstallPackage(
                    It.IsAny<IEnumerable<Project>>(),
                    packageB,
                    It.IsAny<IEnumerable<PackageOperation>>(),
                    false,
                    provider,
                    provider), Times.Never());

                manualEvent.Set();
            };

            var extensionB = new PackageItem(provider, packageB);

            // Act
            provider.Execute(extensionB);

            // do not allow the method to return
            manualEvent.Wait();
        }

        private static SolutionOnlineProvider CreateSolutionOnlineProvider(
            IVsPackageManager packageManager = null,
            IPackageRepository localRepository = null,
            IPackageRepositoryFactory repositoryFactory = null,
            IPackageSourceProvider packageSourceProvider = null,
            IScriptExecutor scriptExecutor = null,
            ISolutionManager solutionManager = null,
            IProjectSelectorService projectSelectorService = null) {

            if (packageManager == null) {
                var packageManagerMock = new Mock<IVsPackageManager>();
                var sourceRepository = new MockPackageRepository();
                packageManagerMock.Setup(p => p.SourceRepository).Returns(sourceRepository);

                packageManager = packageManagerMock.Object;
            }

            if (repositoryFactory == null) {
                var repositoryFactoryMock = new Mock<IPackageRepositoryFactory>();
                repositoryFactoryMock.Setup(p => p.CreateRepository(It.IsAny<string>())).Returns(new MockPackageRepository());
                repositoryFactory = repositoryFactoryMock.Object;
            }

            if (packageSourceProvider == null) {
                var packageSourceProviderMock = new Mock<IPackageSourceProvider>();
                packageSourceProviderMock.Setup(p => p.LoadPackageSources()).Returns(
                        new PackageSource[2] {
                            new PackageSource("Test1", "One"),
                            new PackageSource("Test2", "Two")
                        }
                    );
                packageSourceProvider = packageSourceProviderMock.Object;
            }

            var factory = new Mock<IVsPackageManagerFactory>();
            factory.Setup(m => m.CreatePackageManager(It.IsAny<IPackageRepository>())).Returns(packageManager);

            var mockProgressWindowOpener = new Mock<IProgressWindowOpener>();
            var mockLicenseWindowOpener = new Mock<ILicenseWindowOpener>();

            if (scriptExecutor == null) {
                scriptExecutor = new Mock<IScriptExecutor>().Object;
            }

            if (solutionManager == null) {
                solutionManager = new Mock<ISolutionManager>().Object;
            }

            if (projectSelectorService == null) {
                var mockProjectSelector = new Mock<IProjectSelectorService>();
                mockProjectSelector.Setup(p => p.ShowProjectSelectorWindow(It.IsAny<Func<Project, bool>>(), It.IsAny<Func<Project, bool>>())).Returns(
                    solutionManager.GetProjects()
                );
                projectSelectorService = mockProjectSelector.Object;
            }

            var services = new ProviderServices(
                mockLicenseWindowOpener.Object,
                mockProgressWindowOpener.Object,
                scriptExecutor,
                new MockOutputConsoleProvider(),
                projectSelectorService
            );

            if (localRepository == null) {
                localRepository = new Mock<IPackageRepository>().Object;
            }

            return new SolutionOnlineProvider(
                localRepository,
                new System.Windows.ResourceDictionary(),
                repositoryFactory,
                packageSourceProvider,
                factory.Object,
                services,
                new Mock<IProgressProvider>().Object,
                solutionManager);
        }
    }
}
