from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.bake_options import BakeOptions





T = TypeVar("T", bound="BakeRequest")



@_attrs_define
class BakeRequest:
    """ A high-to-low bake. Everything is a path on the Server's machine, and the results are files there: a client
    sends paths and gets paths, whichever client it is (plan §8).

        Attributes:
            high (list[str]): The detailed meshes, in order: one is usual, and a model split across files lists them all.
            low (str): The mesh baked onto, which must have texture coordinates.
            output (str): The folder the maps are written to.
            options (BakeOptions): How a bake runs, with no meshes in it: what a project keeps between bakes and every
                client sends. Each map's
                own settings are a part of their own.
            cage (None | str | Unset): A mesh with the low mesh's faces, moved outward, which the rays start from.
            material (None | str | Unset): The material of the low mesh to bake, for one texture set of a model that has
                several.
            uv_set (int | None | Unset): The low mesh's texture coordinate set the maps are baked in, by the file's number:
                zero for the first
                (#210). Null bakes a texture set in the set it is laid out in, when the low mesh is a project's and
                string? BakeRequest.Material names one of its texture sets, and in the first otherwise. The high mesh is read
                whole, so its sets do not matter, but for a picture moved from its layout, which is always its first.
            name (None | str | Unset): The stem of each file's name, or null for the low mesh's.
     """

    high: list[str]
    low: str
    output: str
    options: BakeOptions
    cage: None | str | Unset = UNSET
    material: None | str | Unset = UNSET
    uv_set: int | None | Unset = UNSET
    name: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.bake_options import BakeOptions # noqa: PLC0415
        high = self.high



        low = self.low

        output = self.output

        options = self.options.to_dict()

        cage: None | str | Unset
        if isinstance(self.cage, Unset):
            cage = UNSET
        else:
            cage = self.cage

        material: None | str | Unset
        if isinstance(self.material, Unset):
            material = UNSET
        else:
            material = self.material

        uv_set: int | None | Unset
        if isinstance(self.uv_set, Unset):
            uv_set = UNSET
        else:
            uv_set = self.uv_set

        name: None | str | Unset
        if isinstance(self.name, Unset):
            name = UNSET
        else:
            name = self.name


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "high": high,
            "low": low,
            "output": output,
            "options": options,
        })
        if cage is not UNSET:
            field_dict["cage"] = cage
        if material is not UNSET:
            field_dict["material"] = material
        if uv_set is not UNSET:
            field_dict["uvSet"] = uv_set
        if name is not UNSET:
            field_dict["name"] = name

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.bake_options import BakeOptions # noqa: PLC0415
        d = dict(src_dict)
        high = cast(list[str], d.pop("high"))


        low = d.pop("low")

        output = d.pop("output")

        options = BakeOptions.from_dict(d.pop("options"))




        def _parse_cage(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        cage = _parse_cage(d.pop("cage", UNSET))


        def _parse_material(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        material = _parse_material(d.pop("material", UNSET))


        def _parse_uv_set(data: object) -> int | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(int | None | Unset, data)

        uv_set = _parse_uv_set(d.pop("uvSet", UNSET))


        def _parse_name(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        name = _parse_name(d.pop("name", UNSET))


        bake_request = cls(
            high=high,
            low=low,
            output=output,
            options=options,
            cage=cage,
            material=material,
            uv_set=uv_set,
            name=name,
        )


        bake_request.additional_properties = d
        return bake_request

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
