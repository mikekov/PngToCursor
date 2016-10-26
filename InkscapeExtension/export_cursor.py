#!/usr/bin/env python

'''
This program is free software; you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation; either version 2 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
'''
import os
import tempfile
import subprocess
from subprocess import Popen, PIPE
import sys
sys.path.append('/usr/share/inkscape/extensions')

# We will use the inkex module with the predefined Effect base class.
import inkex
from simplestyle import *
from simplepath import *
from math import sqrt

color_props_fill=('fill:','stop-color:','flood-color:','lighting-color:')
color_props_stroke=('stroke:',)
color_props = color_props_fill + color_props_stroke

'''
def correlation(xList,yList):
    #print yList
    n = len(xList)
    sumX = 0
    sumXX = 0
    sumY = 0
    sumYY = 0
    sumXY = 0
    for i in range(0,n):
    	X = xList[i]
        sumX += X
        sumXX += X*X
        Y = yList[i]
        sumY += Y
        sumYY += Y*Y
        sumXY += X*Y
    corrnum = (n * sumXY)-(sumX * sumY)
    corrden = sqrt( (n * sumXX) - (sumX * sumX) ) * sqrt( (n * sumYY) - (sumY * sumY) )
    corr = corrnum/corrden
    return corr

def pathMatch(rPath,cPath):
    n = len(rPath)
    for i in range(0,n):
        rNode = rPath[i]
        cNode = cPath[i]
        [rCmd,rPoints] = rNode
        [cCmd,cPoints] = cNode
        if rCmd != cCmd:
            #print "not match"
            return 0
    #print "Command Match"
    return 1

def pathPullPoints(rPath,cPath):
    n = len(rPath)
    rPointList = []
    cPointList = []
    for i in range(0,n):
        rNode = rPath[i]
        cNode = cPath[i]
        [rCmd,rPoints] = rNode
        [cCmd,cPoints] = cNode
        rPointList.extend(rPoints)
        cPointList.extend(cPoints)
    return [rPointList,cPointList]


def getLayer(svg, layerName):
    for g in svg.xpath('//svg:g', namespaces=inkex.NSS):
        if g.get(inkex.addNS('groupmode', 'inkscape')) == 'layer' \
            and g.get(inkex.addNS('label', 'inkscape')) \
            == layerName:
            return g
    # Create a new layer.
    newLayer = inkex.etree.SubElement(svg, 'g')
    newLayer.set(inkex.addNS('label', 'inkscape'), layerName)
    newLayer.set(inkex.addNS('groupmode', 'inkscape'), 'layer')
    return newLayer

def compareColors(refNode, compNode):
    pass

def getColor(node):
    col = {}
    if 'style' in node.attrib:
        style=node.get('style') # fixme: this will break for presentation attributes!
        if style!='':
            #inkex.debug('old style:'+style)
            styles=style.split(';')
            for i in range(len(styles)):
                for c in range(len(color_props)):
                    if styles[i].startswith(color_props[c]):
                        #print "col num %d" % c
                        #print styles[i][len(color_props[c]):]
                        col[c] =  styles[i][len(color_props[c]):]
    return col

def colorMatch(rNode,cNode):
    rCol = getColor(rNode)
    #print rCol
    cCol = getColor(cNode)
    #print cCol
    if rCol == cCol:
        return 1
    return 0
'''


class ExportCursor(inkex.Effect):
    """
    Inkscape effect extension.
    Exports selected object to PNG and converts it to a cursor file.
    """
    def __init__(self):
        """
        Constructor.
        Defines the "--what" option of a script.
        """
        # Call the base class constructor.
        inkex.Effect.__init__(self)
        self.OptionParser.add_option('-o', '--out_dir', action = 'store',
            type = 'string', dest = 'output_directory', default = 'c:',
            help = 'Destination folder for cursors.')

    def get_cmd_output(self, cmd):
        # This solution comes from Andrew Reedick <jr9445 at ATT.COM>
        # http://mail.python.org/pipermail/python-win32/2008-January/006606.html
        # This method replaces the commands.getstatusoutput() usage, with the
        # hope to correct the windows exporting bug:
        # https://bugs.launchpad.net/inkscape/+bug/563722
        if sys.platform != "win32": cmd = '{ '+ cmd +'; }'
        pipe = os.popen(cmd +' 2>&1', 'r')
        text = pipe.read()
        sts = pipe.close()
        if sts is None: sts = 0
        if text[-1:] == '\n': text = text[:-1]
        return sts, text


    def effect(self):
        """
        Effect behaviour.
        Export selected object to PNG
        """
        #foundLayer = self.options.foundLayer
        #matchcolor = self.options.matchcolor

        # Get access to main SVG document element
        #svg = self.document.getroot()

        # get the layer where the found paths will be moved to
        #layer = getLayer(svg, foundLayer)

        # get a list of all path nodes
        #pathNodes = self.document.xpath('//svg:path',namespaces=inkex.NSS)

        if len(self.selected) == 0:
            print >>sys.stderr, "Nothing is selected."
            return

        if self.options.output_directory == "":
            print >>sys.stderr, "Empty destination folder."
            return

        # setup stderr so that we can print to it for debugging
        saveout = sys.stdout
        sys.stdout = sys.stderr

        elements = { }

        for id, node in self.selected.iteritems():
            svg_file = self.args[-1]

            # has to be one of the canvas objects...
            if id[0:-1] != "canvas-":
                continue

            suffix = id[-1:]

            (ref, tmp_png) = tempfile.mkstemp('.png')
            os.close(ref)

            # export PNG and x/y locations
            command = "inkscape --without-gui --query-all --export-id=%s --export-png \"%s\" \"%s\" " % (id, tmp_png, svg_file)
            (status, output) = self.get_cmd_output(command)

            if status == 0:

                if not elements:
                    for el in output.split('\n'):
                        el = el.split(',')
                        if len(el) == 5:
                            elements[el[0]] = { 'x': float(el[1]), 'y': float(el[2]), 'w': float(el[3]), 'h': float(el[4]) }

                # read location of the hot spot correnponding to the "canvas-<n>" element
                hot_spot = elements["hot-spot-128-" + suffix]
                (x, y) = (hot_spot['x'] + hot_spot['w'] / 2, hot_spot['y'] + hot_spot['h'] / 2)
                hot_spot_list = "%s,%s" % (x, y)

                # read file name; get text from textspan element
                output_file = os.path.join(self.options.output_directory, self.getElementById("filename-" + suffix).getchildren()[0].text)

                params = "\"%s\" \"%s\" \"%s\"" % (tmp_png, output_file, hot_spot_list)
                cmd2 = "PngToIco.exe " + params
                p = subprocess.Popen(cmd2, shell=True)
                rc = p.wait()

            os.remove(tmp_png)

        '''
        for cPathNode in pathNodes:
            cPathList = parsePath(cPathNode.attrib['d'])
            cPathLen = len(cPathList)
            #print cPathLen
            #print cPathList
            if rPathLen == cPathLen:
                #print " Found %d in %s" % (rPathLen,cPathNode)

                #print matchcolor
                colorMatchFlag = colorMatch(rPathNode,cPathNode) == 1 or not matchcolor
                pathMatchFlag = pathMatch(rPathList,cPathList)==1

                if pathMatchFlag and colorMatchFlag:
                    [rList,cList] = pathPullPoints(rPathList,cPathList)
                    corVal = correlation(rList,cList)
                    #print "The correlation was %g" % corVal
                    if corVal > 0.80:
                        layer.append(cPathNode)
        '''



        #print
        #print 'This message will be logged instead of displayed'
        sys.stdout = saveout


# Create effect instance and apply it.
effect = ExportCursor()
effect.affect()
