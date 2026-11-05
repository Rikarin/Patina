from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.graph_compound import GraphCompound
  from ..models.graph_edit_result import GraphEditResult





T = TypeVar("T", bound="GraphExtraction")



@_attrs_define
class GraphExtraction:
    """ A selection saved as a compound. The edit undoes as one step, and the file stays.

        Attributes:
            compound (GraphCompound): A compound a graph can hold, and the graph behind it.
            node (int): The node holding it.
            edit (GraphEditResult): The reply to an edit, an undo, a redo or a save.
     """

    compound: GraphCompound
    node: int
    edit: GraphEditResult
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.graph_compound import GraphCompound # noqa: PLC0415
        from ..models.graph_edit_result import GraphEditResult # noqa: PLC0415
        compound = self.compound.to_dict()

        node = self.node

        edit = self.edit.to_dict()


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "compound": compound,
            "node": node,
            "edit": edit,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.graph_compound import GraphCompound # noqa: PLC0415
        from ..models.graph_edit_result import GraphEditResult # noqa: PLC0415
        d = dict(src_dict)
        compound = GraphCompound.from_dict(d.pop("compound"))




        node = d.pop("node")

        edit = GraphEditResult.from_dict(d.pop("edit"))




        graph_extraction = cls(
            compound=compound,
            node=node,
            edit=edit,
        )


        graph_extraction.additional_properties = d
        return graph_extraction

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
