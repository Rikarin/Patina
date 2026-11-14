from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.bake_border_options import BakeBorderOptions
  from ..models.bake_colour_options import BakeColourOptions
  from ..models.bake_curvature_options import BakeCurvatureOptions
  from ..models.bake_edge_options import BakeEdgeOptions
  from ..models.bake_height_options import BakeHeightOptions
  from ..models.bake_id_options import BakeIdOptions
  from ..models.bake_occlusion_options import BakeOcclusionOptions
  from ..models.bake_position_options import BakePositionOptions
  from ..models.bake_ray_options import BakeRayOptions
  from ..models.bake_rounding_options import BakeRoundingOptions
  from ..models.bake_thickness_options import BakeThicknessOptions





T = TypeVar("T", bound="BakeOptions")



@_attrs_define
class BakeOptions:
    """ How a bake runs, with no meshes in it: what a project keeps between bakes and every client sends. Each map's
    own settings are a part of their own.

        Attributes:
            resolution (int): The atlas's width, in texels, and its height too unless int? BakeOptions.Rows says otherwise.
            format_ (str): `png8`, `png16`, `exr16` or `exr32`.
            supersampling (int): How many samples along each side of a texel.
            gutter (int): How many texels of dilation surround each chart.
            maps (list[str]): Which maps to write: `normal`, `height`, `ao`, `bent`, `curvature`,
                `thickness`, `position`, `world`, `id`, `colour`, `opacity` (where a ray
                found the high mesh), `edges` (a band along its sharp or hard edges) and `islands` (a color per UV
                island of the low mesh). The default is what a project's masks read.
            space (str): The frame of the normal and bent normal maps: `tangent` or `object`.
            rays (BakeRayOptions): How far a bake's rays look from the low mesh, and what they may strike.
            height (BakeHeightOptions): The height map's stored range.
            position (BakePositionOptions): How the position map turns a point on the high poly mesh into a value (#148).
            occlusion (BakeOcclusionOptions): The occlusion rays, which ambient occlusion, the bent normal and thickness
                share.
            thickness (BakeThicknessOptions): The thickness map's own settings.
            curvature (BakeCurvatureOptions): The curvature map's stored range (#148).
            edges (BakeEdgeOptions): Which edges the edges map marks, and the band it draws along them (#148).
            borders (BakeBorderOptions): The borders map's width (#121).
            id (BakeIdOptions): What an ID map labels, and how it colours the labels.
            rounding (BakeRoundingOptions): Edges baked round into the normal map, the bevel a hard-surface model was not
                modelled with.
            colour (BakeColourOptions): Where a colour map's colour comes from.
            rows (int | None | Unset): The atlas's height in texels, or null for as many as int BakeOptions.Resolution: a
                square bake. A texture set
                authored wider than it is tall bakes at its own shape, so its maps sit under its layers unresampled (#147).
            follow_set_shape (bool | None | Unset): Whether a bake of a project's texture set takes its height from the
                set's own shape at
                int BakeOptions.Resolution, in place of int? BakeOptions.Rows, so that a wide set and a square one baked
                together each bake at their own (#253). Null follows too; only false keeps int? BakeOptions.Rows. A bake of no
                texture set keeps int? BakeOptions.Rows, and a finished bake's options say the height it was made at and leave
                this out.
     """

    resolution: int
    format_: str
    supersampling: int
    gutter: int
    maps: list[str]
    space: str
    rays: BakeRayOptions
    height: BakeHeightOptions
    position: BakePositionOptions
    occlusion: BakeOcclusionOptions
    thickness: BakeThicknessOptions
    curvature: BakeCurvatureOptions
    edges: BakeEdgeOptions
    borders: BakeBorderOptions
    id: BakeIdOptions
    rounding: BakeRoundingOptions
    colour: BakeColourOptions
    rows: int | None | Unset = UNSET
    follow_set_shape: bool | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.bake_border_options import BakeBorderOptions # noqa: PLC0415
        from ..models.bake_colour_options import BakeColourOptions # noqa: PLC0415
        from ..models.bake_curvature_options import BakeCurvatureOptions # noqa: PLC0415
        from ..models.bake_edge_options import BakeEdgeOptions # noqa: PLC0415
        from ..models.bake_height_options import BakeHeightOptions # noqa: PLC0415
        from ..models.bake_id_options import BakeIdOptions # noqa: PLC0415
        from ..models.bake_occlusion_options import BakeOcclusionOptions # noqa: PLC0415
        from ..models.bake_position_options import BakePositionOptions # noqa: PLC0415
        from ..models.bake_ray_options import BakeRayOptions # noqa: PLC0415
        from ..models.bake_rounding_options import BakeRoundingOptions # noqa: PLC0415
        from ..models.bake_thickness_options import BakeThicknessOptions # noqa: PLC0415
        resolution = self.resolution

        format_ = self.format_

        supersampling = self.supersampling

        gutter = self.gutter

        maps = self.maps



        space = self.space

        rays = self.rays.to_dict()

        height = self.height.to_dict()

        position = self.position.to_dict()

        occlusion = self.occlusion.to_dict()

        thickness = self.thickness.to_dict()

        curvature = self.curvature.to_dict()

        edges = self.edges.to_dict()

        borders = self.borders.to_dict()

        id = self.id.to_dict()

        rounding = self.rounding.to_dict()

        colour = self.colour.to_dict()

        rows: int | None | Unset
        if isinstance(self.rows, Unset):
            rows = UNSET
        else:
            rows = self.rows

        follow_set_shape: bool | None | Unset
        if isinstance(self.follow_set_shape, Unset):
            follow_set_shape = UNSET
        else:
            follow_set_shape = self.follow_set_shape


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "resolution": resolution,
            "format": format_,
            "supersampling": supersampling,
            "gutter": gutter,
            "maps": maps,
            "space": space,
            "rays": rays,
            "height": height,
            "position": position,
            "occlusion": occlusion,
            "thickness": thickness,
            "curvature": curvature,
            "edges": edges,
            "borders": borders,
            "id": id,
            "rounding": rounding,
            "colour": colour,
        })
        if rows is not UNSET:
            field_dict["rows"] = rows
        if follow_set_shape is not UNSET:
            field_dict["followSetShape"] = follow_set_shape

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.bake_border_options import BakeBorderOptions # noqa: PLC0415
        from ..models.bake_colour_options import BakeColourOptions # noqa: PLC0415
        from ..models.bake_curvature_options import BakeCurvatureOptions # noqa: PLC0415
        from ..models.bake_edge_options import BakeEdgeOptions # noqa: PLC0415
        from ..models.bake_height_options import BakeHeightOptions # noqa: PLC0415
        from ..models.bake_id_options import BakeIdOptions # noqa: PLC0415
        from ..models.bake_occlusion_options import BakeOcclusionOptions # noqa: PLC0415
        from ..models.bake_position_options import BakePositionOptions # noqa: PLC0415
        from ..models.bake_ray_options import BakeRayOptions # noqa: PLC0415
        from ..models.bake_rounding_options import BakeRoundingOptions # noqa: PLC0415
        from ..models.bake_thickness_options import BakeThicknessOptions # noqa: PLC0415
        d = dict(src_dict)
        resolution = d.pop("resolution")

        format_ = d.pop("format")

        supersampling = d.pop("supersampling")

        gutter = d.pop("gutter")

        maps = cast(list[str], d.pop("maps"))


        space = d.pop("space")

        rays = BakeRayOptions.from_dict(d.pop("rays"))




        height = BakeHeightOptions.from_dict(d.pop("height"))




        position = BakePositionOptions.from_dict(d.pop("position"))




        occlusion = BakeOcclusionOptions.from_dict(d.pop("occlusion"))




        thickness = BakeThicknessOptions.from_dict(d.pop("thickness"))




        curvature = BakeCurvatureOptions.from_dict(d.pop("curvature"))




        edges = BakeEdgeOptions.from_dict(d.pop("edges"))




        borders = BakeBorderOptions.from_dict(d.pop("borders"))




        id = BakeIdOptions.from_dict(d.pop("id"))




        rounding = BakeRoundingOptions.from_dict(d.pop("rounding"))




        colour = BakeColourOptions.from_dict(d.pop("colour"))




        def _parse_rows(data: object) -> int | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(int | None | Unset, data)

        rows = _parse_rows(d.pop("rows", UNSET))


        def _parse_follow_set_shape(data: object) -> bool | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(bool | None | Unset, data)

        follow_set_shape = _parse_follow_set_shape(d.pop("followSetShape", UNSET))


        bake_options = cls(
            resolution=resolution,
            format_=format_,
            supersampling=supersampling,
            gutter=gutter,
            maps=maps,
            space=space,
            rays=rays,
            height=height,
            position=position,
            occlusion=occlusion,
            thickness=thickness,
            curvature=curvature,
            edges=edges,
            borders=borders,
            id=id,
            rounding=rounding,
            colour=colour,
            rows=rows,
            follow_set_shape=follow_set_shape,
        )


        bake_options.additional_properties = d
        return bake_options

    @property
    def additional_keys(self) -> list[str]:
        return list(self.additional_properties.keys())

    def __getitem__(self, key: str) -> Any:
        return self.additional_properties[key]

    def __setitem__(self, key: str, value: Any) -> None:
        self.additional_properties[key] = value

    def __delitem__(self, key: str) -> None:
        del self.additional_properties[key]

    def __contains__(self, key: str) -> bool:
        return key in self.additional_properties
