## Overview

This *SAInt_HELICS_interface* project runs electricity and gas networks that are coupled through gas-fired power plants using the [HELICS co-simulation platform](https://docs.helics.org/en/latest/). In this setup the two networks are modeled as independent federates with the SAInt software, while the relevant values and messages are exchanged between the two federates via the HELICS platform. 

This repository includes the adapter code to utilize HELICS with SAInt, as well as data for three case studies (Demo-base, Demo-Alternate, and Belgian), each of them with two different scenarios (normal and compressor outage). Note that the two Demo case studies can be simulated using the trial version of SAInt (availble for free by contacting [encoord](https://www.encoord.com/ContactUs.html)), but the Belgian network requires a full SAInt license. 

Additional details on this interface and the work developing HELICS support for SAInt are available [here](https://www.encoord.com/CaseStudyHELICS.html#top).  

## System requirements 

   - Windows Operating System (any of Windows 7, 8 or 10 running either 32-Bit or 64-Bit versions)
   - SAInt 2.0 (Demo version available for free by contacting [encoord](https://www.encoord.com/ContactUs.html)).
   - Visual Studio (Visual Studio Community is available for free from [Microsoft](https://visualstudio.microsoft.com/free-developer-offers/)).
   - Recommended minimum specifications:
      - 2GHz CPU
      - 4GB RAM
      - 5GB Available HDD space after application installs
      - Network Card
      - 15-inch screen with a resolution of at least 1280x960 pixels
      - USB port

## Setting up the SAInt_HELICS interface 

This user guide describes the steps for setting up and running the *SAInt_HELICS_interface* project on *Visual Studio*. It is divided into four sections:
- [Clone the SAInt_HELICS_interface to your working space](#Clone-the-SAInt_HELICS_interface-to-your-working-space)
- [Loading the visual studio project ](#Loading-the-visual-studio-project)
- [Set the project configuration as a multiple startups](#Set-the-project-configuration-as-a-multiple-startups)
- [Configure the solution platform and run the simulation](#Configure-the-solution-platform-and-run-the-simulation)

#### Clone the SAInt_HELICS_interface to your working space
  1. Open visual studio.
  2. Go to the *Team* tab and then *Manage Connections*. Alternatively, you can also directly access this by opening the *Team Explorer* from the *View* tab. Also, open the *Solution Explorer* from the *View* tab if it is not opened.
  3. In the *Team Explorer*, go to *Local Git Repositories* and click *Clone* (see Figure 1 below).
     
   |![Figure1](ReadMeImages/Figure1.png)| 
   |:--:|
   |<b>Figure 1</b>|

  4. In the dialog box that opens, put the URL address of the SAInt_HELICS_interface. Browse the destination directory to point to your workspace folder. Then click *Clone* and wait until it is completed (see Figure 2). The link for the URL is: https://github.com/NREL/SAInt_HELICS_interface
     
   |![Figure2](ReadMeImages/Figure2.png)|
   |:--:|
   |<b>Figure 2</b>|

  5. Once cloning is completed, the *SAInt_HELICS_interface* appears in your *Team Explorer* (Figure 3). 
     
   |![Figure3](ReadMeImages/Figure3.png)|
   |:--:|
   |<b>Figure 3</b>|
   
#### Loading the visual studio project 
  1. Double click the *SAInt_HELICS_interface* in the *Team Explorer*. Then you will see the *HelicsDotNet.sln* solution file as shown in Figure 4.

   |![Figure4](ReadMeImages/Figure4.png)|
   |:--:|
   |<b>Figure 4</b>|

  2. Double click *HelicsDotNet.sln* to open the project in the *Solution Explorer*. Your *Solution Explorer* will look like as shown in Figure 5.
  
   |![Figure5](ReadMeImages/Figure5.png)|
   |:--:|
   |<b>Figure 5</b>|

  3. There are five projects embeded in the *HelicsDotNet* project.
     - *ElectricFederate* loads the electric network, imports the corresponding scenario definitions and run the simulation.
     - *GasFederate* loads the gas network, imports the corresponding scenario definitions and run the simulation.
     - *HelicsDotNetAPI* provides the API functionality requiered for the cosimulation *HELICS* environment.
     - *SAIntHelicsLib* provides the mapping factory for the coupling technologies. It allows the electric and gas federates to communucate.

#### Set the project configuration as a multiple startups
  1. At the top of the *Solution Explorer* right click on the *Solution 'HelicsDotNet'* and open *Set StartUP Projects* as shown in Figure 6. 
    
   |![Figure6](ReadMeImages/Figure6.png)| 
   |:--:|
   |<b>Figure 6</b>|

  2. In the dialog box that opens, set the *ElectricFederate* and the *GasFederate* to *Start* (see Figure 8). 
    
   |![Figure7](ReadMeImages/Figure7.png)| 
   |:--:|
   |<b>Figure 7</b>|

  3. Press *Apply* and then *OK*.
   
#### Configure the solution platform and run the simulation
  1. Configure the solution platform, which is found next to the *Debug* button, to *x64* as shown in Figure 8. 
    
   |![Figure8](ReadMeImages/Figure8.png)|
   |:--:|
   |<b>Figure 8</b>|

  2. Select your case:
     - Open the *Program.cs* files in the *ElectricFederate* project and uncomment only one case that youwant to simulate.
     - Similarly, open the *Program.cs* file in the *GasFederate* project and uncomment the corresponding case.
     - Figure 9 and Figure 10 show the six cases with the boxes indicating the sections corresponding to each case. As example, the figures show the Demo case beeing selected for simulation.
   
   |![Figure 9](ReadMeImages/Figure9.png)|
   |:--:|
   |<b>Figure 9</b>|
   |![Figure 10](ReadMeImages/Figure10.png)|
   |<b>Figure 10</b>|

  3. Simulate by clicking the green button *Start* in Figure 11.
    
   |![Figure 11](ReadMeImages/Figure11.png)|
   |:--:|
   |<b>Figure 11</b>|

  4. The log files will be displayed on two command windows: one for the electric federate and another for the gas federate.
  5. Once the simulation is completed, the exported solution files will be found in your workspace's *outputs* folder.
