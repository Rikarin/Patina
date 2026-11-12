from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.graph_event_kind import GraphEventKind
from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.graph_delta import GraphDelta
  from ..models.graph_evaluation import GraphEvaluation
  from ..models.graph_history_state import GraphHistoryState
  from ..models.graph_selection import GraphSelection





T = TypeVar("T", bound="GraphEvent")



@_attrs_define
class GraphEvent:
    """ One numbered event of a session. Sequence numbers rise by one, so a client that sees a gap has missed something
    and reads `/events` or the state. Every client gets every event, its own edits included.

        Attributes:
            sequence (int): Its number within the session.
            kind (GraphEventKind): What an event reports.
            revision (int): The document's revision when it was sent.
            history (GraphHistoryState): What the undo history offers.
            label (None | str | Unset): What the edit, undo or redo was called.
            delta (GraphDelta | None | Unset):
            evaluation (GraphEvaluation | None | Unset):
            selection (GraphSelection | None | Unset):
     """

    sequence: int
    kind: GraphEventKind
    revision: int
    history: GraphHistoryState
    label: None | str | Unset = UNSET
    delta: GraphDelta | None | Unset = UNSET
    evaluation: GraphEvaluation | None | Unset = UNSET
    selection: GraphSelection | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.graph_delta import GraphDelta # noqa: PLC0415
        from ..models.graph_evaluation import GraphEvaluation # noqa: PLC0415
        from ..models.graph_history_state import GraphHistoryState # noqa: PLC0415
        from ..models.graph_selection import GraphSelection # noqa: PLC0415
        sequence = self.sequence

        kind = self.kind.value

        revision = self.revision

        history = self.history.to_dict()

        label: None | str | Unset
        if isinstance(self.label, Unset):
            label = UNSET
        else:
            label = self.label

        delta: dict[str, Any] | None | Unset
        if isinstance(self.delta, Unset):
            delta = UNSET
        elif isinstance(self.delta, GraphDelta):
            delta = self.delta.to_dict()
        else:
            delta = self.delta

        evaluation: dict[str, Any] | None | Unset
        if isinstance(self.evaluation, Unset):
            evaluation = UNSET
        elif isinstance(self.evaluation, GraphEvaluation):
            evaluation = self.evaluation.to_dict()
        else:
            evaluation = self.evaluation

        selection: dict[str, Any] | None | Unset
        if isinstance(self.selection, Unset):
            selection = UNSET
        elif isinstance(self.selection, GraphSelection):
            selection = self.selection.to_dict()
        else:
            selection = self.selection


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "sequence": sequence,
            "kind": kind,
            "revision": revision,
            "history": history,
        })
        if label is not UNSET:
            field_dict["label"] = label
        if delta is not UNSET:
            field_dict["delta"] = delta
        if evaluation is not UNSET:
            field_dict["evaluation"] = evaluation
        if selection is not UNSET:
            field_dict["selection"] = selection

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.graph_delta import GraphDelta # noqa: PLC0415
        from ..models.graph_evaluation import GraphEvaluation # noqa: PLC0415
        from ..models.graph_history_state import GraphHistoryState # noqa: PLC0415
        from ..models.graph_selection import GraphSelection # noqa: PLC0415
        d = dict(src_dict)
        sequence = d.pop("sequence")

        kind = GraphEventKind(d.pop("kind"))




        revision = d.pop("revision")

        history = GraphHistoryState.from_dict(d.pop("history"))




        def _parse_label(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        label = _parse_label(d.pop("label", UNSET))


        def _parse_delta(data: object) -> GraphDelta | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                delta_type_1 = GraphDelta.from_dict(data)



                return delta_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(GraphDelta | None | Unset, data)

        delta = _parse_delta(d.pop("delta", UNSET))


        def _parse_evaluation(data: object) -> GraphEvaluation | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                evaluation_type_1 = GraphEvaluation.from_dict(data)



                return evaluation_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(GraphEvaluation | None | Unset, data)

        evaluation = _parse_evaluation(d.pop("evaluation", UNSET))


        def _parse_selection(data: object) -> GraphSelection | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                selection_type_1 = GraphSelection.from_dict(data)



                return selection_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(GraphSelection | None | Unset, data)

        selection = _parse_selection(d.pop("selection", UNSET))


        graph_event = cls(
            sequence=sequence,
            kind=kind,
            revision=revision,
            history=history,
            label=label,
            delta=delta,
            evaluation=evaluation,
            selection=selection,
        )


        graph_event.additional_properties = d
        return graph_event

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
