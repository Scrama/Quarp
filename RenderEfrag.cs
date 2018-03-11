/// <copyright>
///
/// Rewritten in C# by Yury Kiselev, 2010.
///
/// Copyright (C) 1996-1997 Id Software, Inc.
///
/// This program is free software; you can redistribute it and/or
/// modify it under the terms of the GNU General Public License
/// as published by the Free Software Foundation; either version 2
/// of the License, or (at your option) any later version.
/// 
/// This program is distributed in the hope that it will be useful,
/// but WITHOUT ANY WARRANTY; without even the implied warranty of
/// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  
/// 
/// See the GNU General Public License for more details.
/// 
/// You should have received a copy of the GNU General Public License
/// along with this program; if not, write to the Free Software
/// Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
/// </copyright>

using System;
using System.Collections.Generic;
using System.Text;
using OpenTK;

// gl_refrag.c

namespace Quarp
{
    partial class Render
    {
        static EntityT _AddEnt; // r_addent
        static mnode_t _EfragTopNode; // r_pefragtopnode
        static Vector3 _EMins; // r_emins
        static Vector3 _EMaxs; // r_emaxs
        /// <summary>
        /// efrag_t **lastlink changed to object _LastObj
        /// and may be a reference to entity_t, in wich case assign *lastlink to ((entity_t)_LastObj).efrag
        /// or to efrag_t in wich case assign *lastlink value to ((efrag_t)_LastObj).entnext
        /// </summary>
        static object _LastObj; // see comments

        /// <summary>
        /// R_AddEfrags
        /// </summary>
        public static void AddEfrags(EntityT ent)
        {
            if (ent.Model == null)
                return;

            _AddEnt = ent;
            _LastObj = ent; //  lastlink = &ent->efrag;
            _EfragTopNode = null;

            model_t entmodel = ent.Model;
            _EMins = ent.Origin + entmodel.mins;
            _EMaxs = ent.Origin + entmodel.maxs;

            SplitEntityOnNode(Client.cl.worldmodel.nodes[0]);
            ent.Topnode = _EfragTopNode;
        }

        /// <summary>
        /// R_SplitEntityOnNode
        /// </summary>
        static void SplitEntityOnNode(mnodebase_t node)
        {
            if (node.contents == Contents.CONTENTS_SOLID)
                return;

            // add an efrag if the node is a leaf
            if (node.contents < 0)
            {
                if (_EfragTopNode == null)
                    _EfragTopNode = node as mnode_t;

                mleaf_t leaf = (mleaf_t)(object)node;

                // grab an efrag off the free list
                EfragT ef = Client.cl.free_efrags;
                if (ef == null)
                {
                    Con.Print("Too many efrags!\n");
                    return;	// no free fragments...
                }
                Client.cl.free_efrags = Client.cl.free_efrags.Entnext;

                ef.Entity = _AddEnt;

                // add the entity link
                // *lastlink = ef;
                if (_LastObj is EntityT)
                {
                    ((EntityT)_LastObj).Efrag = ef;
                }
                else
                {
                    ((EfragT)_LastObj).Entnext = ef;
                }
                _LastObj = ef; // lastlink = &ef->entnext;
                ef.Entnext = null;

                // set the leaf links
                ef.Leaf = leaf;
                ef.Leafnext = leaf.efrags;
                leaf.efrags = ef;

                return;
            }

            // NODE_MIXED
            mnode_t n = node as mnode_t;
            if (n == null)
                return;
            
            mplane_t splitplane = n.plane;
            int sides = Mathlib.BoxOnPlaneSide(ref _EMins, ref _EMaxs, splitplane);

            if (sides == 3)
            {
                // split on this plane
                // if this is the first splitter of this bmodel, remember it
                if (_EfragTopNode == null)
                    _EfragTopNode = n;
            }

            // recurse down the contacted sides
            if ((sides & 1) != 0)
                SplitEntityOnNode(n.children[0]);

            if ((sides & 2) != 0)
                SplitEntityOnNode(n.children[1]);
        }

        /// <summary>
        /// R_StoreEfrags
        /// FIXME: a lot of this goes away with edge-based
        /// </summary>
        static void StoreEfrags(EfragT ef)
        {
            while (ef != null)
            {
                EntityT pent = ef.Entity;
                model_t clmodel = pent.Model;

                switch (clmodel.type)
                {
                    case modtype_t.mod_alias:
                    case modtype_t.mod_brush:
                    case modtype_t.mod_sprite:
                        if ((pent.Visframe != _frameCount) && (Client.NumVisEdicts < Client.MAX_VISEDICTS))
                        {
                            Client.VisEdicts[Client.NumVisEdicts++] = pent;

                            // mark that we've recorded this entity for this frame
                            pent.Visframe = _frameCount;
                        }

                        ef = ef.Leafnext;
                        break;

                    default:
                        Sys.Error("R_StoreEfrags: Bad entity type {0}\n", clmodel.type);
                        break;
                }
            }
        }
    }
}
