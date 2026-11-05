from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.graph_edit_result import GraphEditResult
from ...models.problem_details import ProblemDetails
from ...types import UNSET, Unset
from typing import cast



def _get_kwargs(
    id: str,
    *,
    force: bool | Unset = False,

) -> dict[str, Any]:
    

    

    params: dict[str, Any] = {}

    params["force"] = force


    params = {k: v for k, v in params.items() if v is not UNSET and v is not None}


    _kwargs: dict[str, Any] = {
        "method": "post",
        "url": "/api/v1/graphs/{id}/save".format(id=quote(str(id), safe=""),),
        "params": params,
    }


    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> GraphEditResult | ProblemDetails | None:
    if response.status_code == 200:
        response_200 = GraphEditResult.from_dict(response.json())



        return response_200

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if response.status_code == 409:
        response_409 = ProblemDetails.from_dict(response.json())



        return response_409

    if response.status_code == 422:
        response_422 = ProblemDetails.from_dict(response.json())



        return response_422

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> Response[GraphEditResult | ProblemDetails]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    force: bool | Unset = False,

) -> Response[GraphEditResult | ProblemDetails]:
    """ Writes the document to its file. A file changed on disk refuses this unless force is set.

    Args:
        id (str):
        force (bool | Unset):  Default: False.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[GraphEditResult | ProblemDetails]
     """


    kwargs = _get_kwargs(
        id=id,
force=force,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    force: bool | Unset = False,

) -> GraphEditResult | ProblemDetails | None:
    """ Writes the document to its file. A file changed on disk refuses this unless force is set.

    Args:
        id (str):
        force (bool | Unset):  Default: False.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        GraphEditResult | ProblemDetails
     """


    return sync_detailed(
        id=id,
client=client,
force=force,

    ).parsed

async def asyncio_detailed(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    force: bool | Unset = False,

) -> Response[GraphEditResult | ProblemDetails]:
    """ Writes the document to its file. A file changed on disk refuses this unless force is set.

    Args:
        id (str):
        force (bool | Unset):  Default: False.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[GraphEditResult | ProblemDetails]
     """


    kwargs = _get_kwargs(
        id=id,
force=force,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    force: bool | Unset = False,

) -> GraphEditResult | ProblemDetails | None:
    """ Writes the document to its file. A file changed on disk refuses this unless force is set.

    Args:
        id (str):
        force (bool | Unset):  Default: False.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        GraphEditResult | ProblemDetails
     """


    return (await asyncio_detailed(
        id=id,
client=client,
force=force,

    )).parsed
