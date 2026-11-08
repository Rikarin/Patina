from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="GraphHistoryState")



@_attrs_define
class GraphHistoryState:
    """ What the undo history offers.

        Attributes:
            can_undo (bool): Whether there is an edit to undo.
            can_redo (bool): Whether there is an edit to redo.
            dirty (bool): Whether the document differs from the file.
            undo_label (None | str | Unset): What undo would take back.
            redo_label (None | str | Unset): What redo would put back.
            step (int | None | Unset): The newest step that stands, or null before the first. Each new step, undo and redo
                changes it, so a client that
                lists the history (HistoryList) reads it again when this changes.
     """

    can_undo: bool
    can_redo: bool
    dirty: bool
    undo_label: None | str | Unset = UNSET
    redo_label: None | str | Unset = UNSET
    step: int | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        can_undo = self.can_undo

        can_redo = self.can_redo

        dirty = self.dirty

        undo_label: None | str | Unset
        if isinstance(self.undo_label, Unset):
            undo_label = UNSET
        else:
            undo_label = self.undo_label

        redo_label: None | str | Unset
        if isinstance(self.redo_label, Unset):
            redo_label = UNSET
        else:
            redo_label = self.redo_label

        step: int | None | Unset
        if isinstance(self.step, Unset):
            step = UNSET
        else:
            step = self.step


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "canUndo": can_undo,
            "canRedo": can_redo,
            "dirty": dirty,
        })
        if undo_label is not UNSET:
            field_dict["undoLabel"] = undo_label
        if redo_label is not UNSET:
            field_dict["redoLabel"] = redo_label
        if step is not UNSET:
            field_dict["step"] = step

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        can_undo = d.pop("canUndo")

        can_redo = d.pop("canRedo")

        dirty = d.pop("dirty")

        def _parse_undo_label(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        undo_label = _parse_undo_label(d.pop("undoLabel", UNSET))


        def _parse_redo_label(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        redo_label = _parse_redo_label(d.pop("redoLabel", UNSET))


        def _parse_step(data: object) -> int | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(int | None | Unset, data)

        step = _parse_step(d.pop("step", UNSET))


        graph_history_state = cls(
            can_undo=can_undo,
            can_redo=can_redo,
            dirty=dirty,
            undo_label=undo_label,
            redo_label=redo_label,
            step=step,
        )


        graph_history_state.additional_properties = d
        return graph_history_state

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
